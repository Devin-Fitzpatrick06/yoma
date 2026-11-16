// A minimal one-page text PDF, so a practice sample can be Yoma "Certificate" evidence (Yoma accepts pdf, docx, images)
// without adding a PDF library. Standard Helvetica, WinAnsi text only.

const WIDTH = 595; // A4 in points
const HEIGHT = 842;
const MARGIN = 56;
const CHARS_PER_LINE = 88;

/** Keeps text inside Helvetica's WinAnsi set; the naira sign becomes "NGN". */
function toAnsi(text: string): string {
  return text
    .replace(/₦\s?/g, "NGN ")
    .replace(/[‘’]/g, "'")
    .replace(/[“”]/g, '"')
    .replace(/[–—]/g, "-")
    .replace(/[^\x20-\x7e]/g, "?");
}

function wrap(text: string): string[] {
  const lines: string[] = [];
  for (const para of text.split("\n")) {
    let line = "";
    for (const word of para.split(/\s+/)) {
      if (line && line.length + word.length + 1 > CHARS_PER_LINE) {
        lines.push(line);
        line = "";
      }
      line = line ? `${line} ${word}` : word;
    }
    lines.push(line);
  }
  return lines;
}

const escape = (s: string) => s.replace(/[\\()]/g, (c) => `\\${c}`);

/** Each block is [size, text]. Text past the end of the page is cut, which the 2000-character answer limit keeps away. */
export function textPdf(blocks: [size: number, text: string][]): Uint8Array {
  const ops: string[] = ["BT"];
  let y = HEIGHT - MARGIN;
  for (const [size, text] of blocks) {
    for (const line of wrap(toAnsi(text))) {
      y -= size * 1.4;
      if (y < MARGIN) break;
      ops.push(`/F1 ${size} Tf 1 0 0 1 ${MARGIN} ${y.toFixed(1)} Tm (${escape(line)}) Tj`);
    }
    y -= size * 0.6;
  }
  ops.push("ET");
  const stream = ops.join("\n");

  const objects = [
    "<< /Type /Catalog /Pages 2 0 R >>",
    "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
    `<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ${WIDTH} ${HEIGHT}] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>`,
    "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
    `<< /Length ${stream.length} >>\nstream\n${stream}\nendstream`,
  ];
  let pdf = "%PDF-1.4\n";
  const offsets: number[] = [];
  objects.forEach((body, i) => {
    offsets.push(pdf.length);
    pdf += `${i + 1} 0 obj\n${body}\nendobj\n`;
  });
  const xref = pdf.length;
  pdf += `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n`;
  pdf += offsets.map((o) => `${String(o).padStart(10, "0")} 00000 n \n`).join("");
  pdf += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`;
  return new TextEncoder().encode(pdf); // ASCII only, so string length == byte length for the offsets above
}
