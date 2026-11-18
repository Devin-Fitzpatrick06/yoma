"use client";

import { useRef, useState } from "react";
import { compressImage } from "../lib/compressImage";
import { t } from "../lib/i18n";
import type { Lang } from "../lib/schema";
import { Icon, type IconName } from "./Icon";

type Mode = "type" | "speak" | "photo";
const MODE_ICON: Record<Mode, IconName> = { type: "pencil", speak: "mic", photo: "camera" };

// Web Speech API is not in TypeScript's DOM types; this is the part we use.
interface Recognition {
  lang: string;
  continuous: boolean;
  onerror: (e: { error: string }) => void;
  onresult: (e: { resultIndex: number; results: { 0: { transcript: string }; isFinal: boolean }[] }) => void;
  onend: () => void;
  start(): void;
  stop(): void;
}
type RecognitionCtor = new () => Recognition;

function getRecognition(): RecognitionCtor | undefined {
  const w = window as unknown as { SpeechRecognition?: RecognitionCtor; webkitSpeechRecognition?: RecognitionCtor };
  return w.SpeechRecognition ?? w.webkitSpeechRecognition;
}

interface Props {
  lang: Lang;
  value: string;
  onChange: (value: string) => void;
}

/** Every mode fills the same text box, so the learner can always read and edit what will be sent. */
export function AnswerInput({ lang, value, onChange }: Props) {
  const [mode, setMode] = useState<Mode>("type");
  const [listening, setListening] = useState(false);
  const [speechError, setSpeechError] = useState(false);
  const [photoState, setPhotoState] = useState<"idle" | "reading" | "done" | "error" | "flagged">("idle");
  const recognition = useRef<Recognition | null>(null);
  const append = (text: string) => onChange(value ? `${value.trimEnd()} ${text}` : text);

  function toggleSpeech() {
    if (listening) return recognition.current?.stop();
    const Ctor = getRecognition();
    if (!Ctor) return;
    const r = new Ctor();
    r.lang = "en-NG"; // no Pidgin recogniser exists; Nigerian English is the closest
    r.continuous = true;
    let base = value;
    r.onresult = (e) => {
      for (let i = e.resultIndex; i < e.results.length; i++)
        if (e.results[i].isFinal) {
          base = base ? `${base.trimEnd()} ${e.results[i][0].transcript}` : e.results[i][0].transcript;
          onChange(base);
        }
    };
    r.onend = () => setListening(false);
    // "no-speech", "not-allowed" (microphone blocked) or "network" (Chrome's recogniser is online-only)
    r.onerror = () => {
      setSpeechError(true);
      setListening(false);
    };
    setSpeechError(false);
    recognition.current = r;
    r.start();
    setListening(true);
  }

  async function readPhoto(file: File) {
    setPhotoState("reading");
    try {
      const form = new FormData();
      form.append("photo", await compressImage(file), "answer.jpg");
      const res = await fetch("/api/transcribe", { method: "POST", body: form });
      if (!res.ok) throw new Error();
      const body = (await res.json()) as { status: "ok"; text: string } | { status: "flagged" };
      if (body.status === "flagged") return setPhotoState("flagged");
      append(body.text);
      setPhotoState("done");
    } catch {
      setPhotoState("error");
    }
  }

  const tab = (m: Mode) =>
    `inline-flex min-h-12 flex-1 items-center justify-center gap-1.5 rounded-full font-heading text-lg tracking-wide transition ${mode === m ? "bg-brand text-white shadow-sm" : "text-brand hover:bg-white"}`;

  return (
    <div className="space-y-3">
      <div role="group" aria-label={`${t(lang, "type")} / ${t(lang, "speak")} / ${t(lang, "photo")}`} className="flex gap-1 rounded-full bg-brand-tint p-1">
        {(["type", "speak", "photo"] as const).map((m) => (
          <button key={m} type="button" aria-pressed={mode === m} className={tab(m)} onClick={() => setMode(m)}>
            <Icon name={MODE_ICON[m]} className="size-5" /> {t(lang, m)}
          </button>
        ))}
      </div>

      {mode === "speak" &&
        (typeof window !== "undefined" && getRecognition() ? (
          <button type="button" className="btn-secondary" onClick={toggleSpeech}>
            <Icon name="mic" /> {listening ? t(lang, "speakStop") : t(lang, "speakStart")}
          </button>
        ) : (
          <p role="status">{t(lang, "speakUnsupported")}</p>
        ))}
      {mode === "speak" && speechError && <p role="alert">{t(lang, "speakError")}</p>}

      {mode === "photo" && (
        <div className="space-y-2">
          <label className="btn-secondary cursor-pointer">
            <Icon name="camera" /> {t(lang, "photoLabel")}
            <input
              type="file"
              accept="image/*"
              capture="environment"
              className="sr-only"
              onChange={(e) => e.target.files?.[0] && readPhoto(e.target.files[0])}
            />
          </label>
          <p role="status">
            {photoState === "reading" && t(lang, "photoReading")}
            {photoState === "done" && t(lang, "photoCheck")}
            {photoState === "error" && t(lang, "error")}
            {photoState === "flagged" && t(lang, "photoSafety")}
          </p>
        </div>
      )}

      <label className="block">
        <span className="font-heading text-lg text-brand">{t(lang, "answerLabel")}</span>
        <textarea
          aria-label={t(lang, "answerLabel")}
          className="mt-1 block min-h-40 w-full rounded-2xl border-2 border-brand-soft p-4 text-lg focus:border-brand"
          value={value}
          maxLength={2000}
          onChange={(e) => onChange(e.target.value)}
          aria-describedby="answer-count"
        />
      </label>
      <p id="answer-count" className="text-right text-sm text-ink/70">
        {value.length} / 2000 {t(lang, "characters")}
      </p>
    </div>
  );
}
