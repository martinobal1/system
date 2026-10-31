// Čítanie textu z PDF v prehliadači (pdf.js) — PDF sa nikam neposiela, AI dostane len text špecifikácií.
const PDFJS_BASE = 'https://unpkg.com/pdfjs-dist@3.11.174/build/';
let loading = null;

function loadScript(src) {
  return new Promise(function (resolve, reject) {
    var s = document.createElement('script');
    s.src = src;
    s.async = true;
    s.onload = resolve;
    s.onerror = function () { reject(new Error('Nepodarilo sa načítať knižnicu PDF.')); };
    document.head.appendChild(s);
  });
}

async function ensurePdfJs() {
  if (window.pdfjsLib && window.pdfjsLib.GlobalWorkerOptions.workerSrc) return window.pdfjsLib;
  if (!loading) {
    loading = (async function () {
      if (!window.pdfjsLib) await loadScript(PDFJS_BASE + 'pdf.min.js');
      // Worker z inej domény prehliadač nespustí — načítame ho ako blob.
      const code = await fetch(PDFJS_BASE + 'pdf.worker.min.js').then(function (r) {
        if (!r.ok) throw new Error('Nepodarilo sa načítať knižnicu PDF.');
        return r.text();
      });
      window.pdfjsLib.GlobalWorkerOptions.workerSrc = URL.createObjectURL(new Blob([code], { type: 'text/javascript' }));
      return window.pdfjsLib;
    })().catch(function (err) { loading = null; throw err; });
  }
  return loading;
}

/** Vráti text všetkých strán PDF; onProgress(strana, spolu). */
export async function extractPdfText(arrayBuffer, onProgress) {
  const pdfjs = await ensurePdfJs();
  const pdf = await pdfjs.getDocument({ data: new Uint8Array(arrayBuffer) }).promise;
  const parts = [];
  for (let n = 1; n <= pdf.numPages; n++) {
    const page = await pdf.getPage(n);
    const tc = await page.getTextContent();
    let line = '';
    const lines = [];
    tc.items.forEach(function (it) {
      line += it.str;
      if (it.hasEOL) { lines.push(line); line = ''; } else if (it.str && !/\s$/.test(it.str)) { line += ' '; }
    });
    if (line.trim()) lines.push(line);
    parts.push('--- strana ' + n + ' ---\n' + lines.map(function (l) { return l.replace(/\s+/g, ' ').trim(); }).filter(Boolean).join('\n'));
    if (onProgress) onProgress(n, pdf.numPages);
  }
  return { text: parts.join('\n\n'), pages: pdf.numPages };
}

/** Súbor → Base64 (bez prefixu data:). */
export function fileToBase64(file) {
  return new Promise(function (resolve, reject) {
    const r = new FileReader();
    r.onload = function () { resolve(String(r.result).replace(/^data:[^,]*,/, '')); };
    r.onerror = function () { reject(new Error('Súbor sa nepodarilo prečítať.')); };
    r.readAsDataURL(file);
  });
}
