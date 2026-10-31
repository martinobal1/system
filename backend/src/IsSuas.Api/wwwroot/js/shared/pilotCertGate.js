// Pilot (úroveň 3) bez platného osvedčenia nemôže žiadať o let ani zapisovať lety.
// Namiesto toho vidí tlačidlo „Požiadať o osvedčenie na lietanie“.
import { api } from '../core/api.js';
import { state } from '../core/state.js';

const CERT_LABELS = { A1_A3: 'A1/A3', A2: 'A2', STS1: 'STS1', STS2: 'STS2' };

function esc(v) {
  return String(v == null ? '' : v).replace(/[&<>"']/g, function (c) {
    return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
  });
}

/** Vráti { pilot, certified, requested } pre prihláseného pilota (L3); pre L1/L2 vždy certified. */
export async function loadMyPilotCertState() {
  if (Number(state.accessLevel) !== 3) return { pilot: null, certified: true, requested: [] };
  var pilots = [];
  try { pilots = await api('/api/v1/pilots'); } catch (_) { pilots = []; }
  var pid = String(state.personId || '').toLowerCase();
  var me = (pilots || []).find(function (p) {
    return pid && String(p.personId || '').toLowerCase() === pid && p.isActive !== false;
  }) || null;
  return certStateFor(me);
}

/** Stav osvedčenia pre už načítaný záznam pilota. */
export function certStateFor(me) {
  return {
    pilot: me,
    certified: !!(me && me.certificateCodes && me.certificateCodes.length),
    requested: (me && me.requestedCertificateCodes) || []
  };
}

/** HTML panelu pre pilota bez osvedčenia. */
export function certGateHtml(info) {
  var requested = info.requested || [];
  if (!info.pilot) {
    return '<div class="cert-gate"><div><b>Nie ste v registri PnD.</b>' +
      '<span>Bez záznamu v registri a platného osvedčenia nemôžete žiadať o let. Obráťte sa na správcu útvaru.</span></div></div>';
  }
  if (requested.length) {
    return '<div class="cert-gate is-waiting"><div><b>Žiadosť o osvedčenie čaká na vybavenie.</b>' +
      '<span>Požiadali ste o: ' + requested.map(function (c) { return esc(CERT_LABELS[c] || c); }).join(', ') +
      '. O let môžete žiadať, keď vám gestor alebo správca útvaru osvedčenie pridelí.</span></div></div>';
  }
  return '<div class="cert-gate"><div><b>Nemáte osvedčenie na lietanie.</b>' +
    '<span>O let môžete žiadať až s platným osvedčením (preukazom PnD).</span></div>' +
    '<button type="button" id="cert-gate-open">Požiadať o osvedčenie na lietanie</button></div>' +
    '<div id="cert-gate-panel" class="form-panel hidden"><h3>Žiadosť o osvedčenie na lietanie</h3>' +
    '<form id="cert-gate-form" class="form-grid">' +
    '<label>Kategória<select id="cert-gate-type">' +
    '<option value="A1_A3">A1/A3 — otvorená kategória</option>' +
    '<option value="A2">A2 — otvorená kategória</option>' +
    '<option value="STS1">STS1 — štandardný scenár</option>' +
    '<option value="STS2">STS2 — štandardný scenár</option>' +
    '</select></label>' +
    '<label>Poznámka<input id="cert-gate-note" maxlength="400" placeholder="voliteľné, napr. termín skúšky" /></label>' +
    '<div class="full toolbar"><button type="submit">Odoslať žiadosť</button>' +
    '<button type="button" class="secondary" id="cert-gate-cancel">Zrušiť</button></div>' +
    '</form><div id="cert-gate-msg"></div></div>';
}

/** Oživí tlačidlá panelu; po odoslaní zavolá onDone(). */
export function bindCertGate(onDone) {
  var openBtn = document.getElementById('cert-gate-open');
  var panel = document.getElementById('cert-gate-panel');
  if (!openBtn || !panel) return;
  openBtn.onclick = function () {
    panel.classList.remove('hidden');
    openBtn.classList.add('hidden');
    document.getElementById('cert-gate-type').focus();
  };
  document.getElementById('cert-gate-cancel').onclick = function () {
    panel.classList.add('hidden');
    openBtn.classList.remove('hidden');
  };
  document.getElementById('cert-gate-form').onsubmit = async function (ev) {
    ev.preventDefault();
    var msg = document.getElementById('cert-gate-msg');
    try {
      await api('/api/v1/pilots/me/certificate-request', {
        method: 'POST',
        body: JSON.stringify({
          certificateTypeCode: document.getElementById('cert-gate-type').value,
          note: document.getElementById('cert-gate-note').value.trim() || null
        })
      });
      if (onDone) onDone();
    } catch (err) {
      msg.innerHTML = '<div class="error">' + esc(err.message || 'Žiadosť sa nepodarilo odoslať') + '</div>';
    }
  };
}
