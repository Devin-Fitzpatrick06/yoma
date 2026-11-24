import { useState } from "react";
import { IoMdCheckmarkCircle, IoMdCloseCircle } from "react-icons/io";
import type {
  CourseFormat,
  CourseQualityAssessment,
} from "~/api/models/courseQuality";
import {
  BTN_DANGER,
  BTN_PRIMARY,
  BTN_SECONDARY,
} from "~/components/Common/buttonStyles";
import CustomModal from "~/components/Common/CustomModal";
import {
  MODAL_ACTION_WIDTH,
  ModalActions,
  ModalBody,
  ModalHeader,
} from "~/components/Common/ModalChrome";
import {
  type FinalStatus,
  getDecisionLabel,
  needsFormat,
} from "./reviewLabels";

const MAX_NOTE_LENGTH = 1000;

export interface ReviewDecisionInput {
  /** Null keeps the detected format. */
  format: CourseFormat | null;
  note: string | null;
}

export interface PendingDecision {
  item: CourseQualityAssessment;
  target: FinalStatus;
}

/** The form inside the dialog; keyed by the decision, so its fields reset per course. */
const DecisionForm: React.FC<{
  decision: PendingDecision;
  isPending: boolean;
  onClose: () => void;
  onSubmit: (input: ReviewDecisionInput) => void;
}> = ({ decision, isPending, onClose, onSubmit }) => {
  const detected = decision.item.learnerCard.format;
  const [format, setFormat] = useState<CourseFormat>(detected);
  const [note, setNote] = useState("");

  // the API rejects Verified while the format is still Unknown
  const isFormatRequired =
    decision.target === "Verified" && needsFormat(decision.item);
  const isFormatMissing = isFormatRequired && format === "Unknown";

  return (
    <form
      className="flex h-full flex-col overflow-y-auto"
      onSubmit={(e) => {
        e.preventDefault();
        if (isPending || isFormatMissing) return;
        onSubmit({
          format: format === detected ? null : format,
          note: note.trim() || null,
        });
      }}
    >
      <ModalHeader
        title={getDecisionLabel(decision.item.status, decision.target)}
        icon={
          decision.target === "Verified" ? (
            <IoMdCheckmarkCircle className="text-success h-5 w-5" />
          ) : (
            <IoMdCloseCircle className="text-error h-5 w-5" />
          )
        }
        onClose={onClose}
      />
      <ModalBody className="flex flex-col gap-3">
        <p className="w-full text-sm break-words">
          {decision.item.opportunityTitle}
        </p>
        <label className="flex w-full flex-col gap-1 text-sm">
          <span className="font-semibold">
            Format{isFormatRequired ? "" : " (optional)"}
          </span>
          <select
            className="select select-sm w-full"
            value={format}
            disabled={isPending}
            aria-invalid={isFormatMissing}
            onChange={(e) => setFormat(e.target.value as CourseFormat)}
          >
            {detected === "Unknown" && (
              <option value="Unknown">Select a format</option>
            )}
            <option value="Course">Course</option>
            <option value="Webinar">Webinar</option>
          </select>
          {isFormatMissing && (
            <span className="text-error text-xs">
              Choose Course or Webinar before marking this Verified.
            </span>
          )}
        </label>
        <label className="flex w-full flex-col gap-1 text-sm">
          <span className="font-semibold">Note (optional)</span>
          <textarea
            className="textarea textarea-bordered w-full"
            rows={3}
            maxLength={MAX_NOTE_LENGTH}
            value={note}
            disabled={isPending}
            onChange={(e) => setNote(e.target.value)}
          />
        </label>
      </ModalBody>
      <ModalActions>
        <button
          type="button"
          className={`${BTN_SECONDARY} ${MODAL_ACTION_WIDTH}`}
          disabled={isPending}
          onClick={onClose}
        >
          Cancel
        </button>
        <button
          type="submit"
          className={`${
            decision.target === "Verified" ? BTN_PRIMARY : BTN_DANGER
          } ${MODAL_ACTION_WIDTH}`}
          disabled={isPending || isFormatMissing}
        >
          {isPending ? "Saving..." : "Save decision"}
        </button>
      </ModalActions>
    </form>
  );
};

/** Confirm or override an assessment, with an optional format and an internal note. */
const ReviewDecisionDialog: React.FC<{
  decision: PendingDecision | null;
  isPending: boolean;
  onClose: () => void;
  onSubmit: (input: ReviewDecisionInput) => void;
}> = ({ decision, isPending, onClose, onSubmit }) => (
  <CustomModal
    isOpen={decision != null}
    shouldCloseOnOverlayClick={!isPending}
    onRequestClose={() => {
      if (!isPending) onClose();
    }}
    className="md:max-h-[540px] md:w-[500px]"
  >
    {decision && (
      <DecisionForm
        key={`${decision.item.id}-${decision.target}`}
        decision={decision}
        isPending={isPending}
        onClose={() => {
          if (!isPending) onClose();
        }}
        onSubmit={onSubmit}
      />
    )}
  </CustomModal>
);

export default ReviewDecisionDialog;
