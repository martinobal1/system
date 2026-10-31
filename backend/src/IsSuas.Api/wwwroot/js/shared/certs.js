import { state } from '../core/state.js';
import { api } from '../core/api.js';
import { trashButtonHtml } from '../core/trash.js';
import { addDaysLocal, content, isoDateLocal, statusBadge } from '../core/ui.js';

export function isCertIssued(c) {
  var st = c && c.status;
  return st === 'Issued' || st === 1;
}

export function isCertSuspended(c) {
  var st = c && c.status;
  return st === 'Suspended' || st === 2;
}

export function certStatusHtml(c) {
  if (isCertSuspended(c)) {
    var until = c.suspendedUntil ? new Date(c.suspendedUntil).toLocaleDateString('sk-SK') : '';
    return statusBadge('Suspended') + (until ? ' <span class="muted">do ' + until + '</span>' : '');
  }
  return statusBadge('Issued');
}

export function certCascadeHint(code, action) {
  var t = String(code || '').toUpperCase().replace(/[-\/\s]/g, '_');
  var extra = t === 'A1_A3' || t === 'A1A3' ? 'A2, STS1 a STS2'
    : t === 'A2' ? 'STS1 a STS2'
    : t === 'STS1' ? 'STS2' : '';
  if (!extra) return '';
  return action === 'suspend'
    ? ' Pozastavia sa aj ' + extra + '.'
    : ' Odoberú sa aj ' + extra + '.';
}

export function certActionButtons(pilotId, c, name) {
  if (state.accessLevel !== 1 && state.accessLevel !== 2) return '';
  var cid = (c && c.id) || '';
  if (!cid) return '';
  var code = c.certificateTypeCode || '';
  var safe = String(name || '').replace(/"/g, '&quot;');
  var html = '';
  if (isCertIssued(c)) {
    html += ' <button type="button" class="secondary cert-suspend" data-pilot="' + pilotId +
      '" data-id="' + cid + '" data-code="' + code + '" data-name="' + safe + '">Pozastaviť</button>';
  }
  if (isCertSuspended(c)) {
    html += ' <button type="button" class="secondary cert-resume" data-pilot="' + pilotId +
      '" data-id="' + cid + '">Obnoviť</button>';
  }
  html += ' <button type="button" class="secondary cert-revoke" data-pilot="' + pilotId +
    '" data-id="' + cid + '" data-code="' + code + '" data-name="' + safe + '">Odobrať</button>';
  html += ' ' + trashButtonHtml('certificates', cid, (c.titleSk || code) + ' – ' + (name || ''), { compact: true });
  return html;
}

export function certSuspendPanelHtml() {
  return '<div id="cert-suspend-panel" class="form-panel hidden">' +
    '<h3>Pozastaviť osvedčenie</h3>' +
    '<p id="cert-suspend-who" class="muted"></p>' +
    '<div class="form-grid">' +
    '<label>Pozastaviť do<input id="cert-suspend-until" type="date" required /></label>' +
    '</div>' +
    '<p class="muted" style="margin:0.4rem 0 0">Skratka vyplní dátum v kalendári:</p>' +
    '<div class="toolbar" style="margin-top:0.35rem">' +
    '<button type="button" class="secondary cert-suspend-preset" data-days="7">1 týždeň</button>' +
    '<button type="button" class="secondary cert-suspend-preset" data-days="30">1 mesiac</button>' +
    '<button type="button" class="secondary cert-suspend-preset" data-days="90">3 mesiace</button>' +
    '<button type="button" class="secondary cert-suspend-preset" data-days="180">6 mesiacov</button>' +
    '</div>' +
    '<div class="toolbar">' +
    '<button type="button" id="cert-suspend-ok">Pozastaviť</button>' +
    '<button type="button" class="secondary" id="cert-suspend-cancel">Zrušiť</button>' +
    '</div></div>';
}

export function bindCertLifecycleButtons(onDone) {
  var pending = null;
  var panel = document.getElementById('cert-suspend-panel');
  var who = document.getElementById('cert-suspend-who');
  var untilInput = document.getElementById('cert-suspend-until');
  var ok = document.getElementById('cert-suspend-ok');
  var cancel = document.getElementById('cert-suspend-cancel');
  function fillUntil(days) {
    if (!untilInput) return;
    untilInput.min = isoDateLocal(addDaysLocal(1));
    untilInput.value = isoDateLocal(addDaysLocal(days));
  }
  function hidePanel() {
    pending = null;
    if (panel) panel.classList.add('hidden');
  }
  content.querySelectorAll('.cert-suspend').forEach(function (btn) {
    btn.onclick = function () {
      pending = { pilot: btn.dataset.pilot, id: btn.dataset.id, code: btn.dataset.code, name: btn.dataset.name };
      fillUntil(30);
      if (who) {
        who.textContent = (pending.name || '') + ' · ' + (pending.code || '').replace('A1_A3', 'A1/A3') +
          '.' + certCascadeHint(pending.code, 'suspend');
      }
      if (panel) {
        panel.classList.remove('hidden');
        panel.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
      }
      if (untilInput) untilInput.focus();
    };
  });
  content.querySelectorAll('.cert-suspend-preset').forEach(function (btn) {
    btn.onclick = function () {
      fillUntil(Number(btn.dataset.days) || 30);
    };
  });
  if (ok) {
    ok.onclick = async function () {
      if (!pending) return;
      var dateVal = untilInput && untilInput.value;
      if (!dateVal) {
        await onDone(null, 'Vyberte dátum v kalendári, dokedy má byť osvedčenie pozastavené.');
        return;
      }
      var until = new Date(dateVal + 'T23:59:59');
      if (!(until.getTime() > Date.now())) {
        await onDone(null, 'Dátum musí byť v budúcnosti.');
        return;
      }
      try {
        await api('/api/v1/pilots/' + pending.pilot + '/certificates/' + pending.id + '/suspend', {
          method: 'POST',
          body: JSON.stringify({
            until: until.toISOString(),
            reason: 'Pozastavené do ' + until.toLocaleDateString('sk-SK')
          })
        });
        hidePanel();
        await onDone('Osvedčenie pozastavené do ' + until.toLocaleDateString('sk-SK') + '.');
      } catch (err) {
        await onDone(null, err.message || 'Pozastavenie zlyhalo');
      }
    };
  }
  if (cancel) cancel.onclick = hidePanel;
  content.querySelectorAll('.cert-resume').forEach(function (btn) {
    btn.onclick = async function () {
      if (!confirm('Obnoviť platnosť osvedčenia?')) return;
      try {
        await api('/api/v1/pilots/' + btn.dataset.pilot + '/certificates/' + btn.dataset.id + '/resume', {
          method: 'POST', body: '{}'
        });
        await onDone('Osvedčenie obnovené.');
      } catch (err) {
        await onDone(null, err.message || 'Obnovenie zlyhalo');
      }
    };
  });
  content.querySelectorAll('.cert-revoke').forEach(function (btn) {
    btn.onclick = async function () {
      if (!confirm('Odobrať osvedčenie' + (btn.dataset.name ? ' pilotovi ' + btn.dataset.name : '') +
          '?' + certCascadeHint(btn.dataset.code, 'revoke'))) return;
      try {
        await api('/api/v1/pilots/' + btn.dataset.pilot + '/certificates/' + btn.dataset.id + '/revoke', {
          method: 'POST',
          body: JSON.stringify({ reason: 'Odobraté' })
        });
        await onDone('Osvedčenie odobraté.');
      } catch (err) {
        await onDone(null, err.message || 'Odobratie zlyhalo');
      }
    };
  });
}

// ---------- Prideliť / zmeniť osvedčenie (detail pilota, stránka Osvedčenia) ----------
function escC(v) {
  return String(v == null ? '' : v).replace(/[&<>"']/g, function (ch) {
    return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch];
  });
}

/** Formulár na pridelenie alebo zmenu osvedčenia jednému pilotovi (L1/L2). */
export function certGrantFormHtml(pilotName, current) {
  if (state.accessLevel !== 1 && state.accessLevel !== 2) return '';
  var cur = current || {};
  function opt(v, label) {
    return '<option value="' + v + '"' + (cur.code === v ? ' selected' : '') + '>' + label + '</option>';
  }
  return '<div class="form-panel" id="pcg-panel">' +
    '<h3 style="margin:0 0 0.3rem">Prideliť / zmeniť osvedčenie</h3>' +
    '<p class="muted" style="margin:0 0 0.75rem">Pilot: <b>' + escC(pilotName) + '</b>. Vyššie osvedčenie doplní aj nižšie ' +
    '(STS2 → STS1 + A2 + A1/A3). Pridelenie rovnakej kategórie znova nahradí číslo a platnosť.</p>' +
    '<form id="pcg-form" class="form-grid">' +
    '<label>Kategória<select id="pcg-type">' +
    opt('A1_A3', 'A1/A3 — otvorená kategória') + opt('A2', 'A2 — otvorená kategória') +
    opt('STS1', 'STS1 — štandardný scenár') + opt('STS2', 'STS2 — štandardný scenár') +
    '</select></label>' +
    '<label>Číslo osvedčenia<input id="pcg-number" placeholder="voliteľné" value="' + escC(cur.number || '') + '" /></label>' +
    '<label>Platnosť od<input id="pcg-from" type="date" value="' + escC(cur.from || '') + '" /></label>' +
    '<label>Platnosť do<input id="pcg-to" type="date" value="' + escC(cur.to || '') + '" /></label>' +
    '<label class="full">MTOW / text platnosti<input id="pcg-mtow" placeholder="voliteľné, napr. MTOW 25 kg" value="' + escC(cur.mtow || '') + '" /></label>' +
    '<div class="full toolbar"><button type="submit">Uložiť osvedčenie</button></div>' +
    '</form><div id="pcg-msg"></div></div>';
}

/** Oživí formulár; onDone(message) po úspechu. */
export function bindCertGrantForm(pilotId, onDone) {
  var form = document.getElementById('pcg-form');
  if (!form) return;
  form.onsubmit = async function (ev) {
    ev.preventDefault();
    var msg = document.getElementById('pcg-msg');
    var from = document.getElementById('pcg-from').value;
    var to = document.getElementById('pcg-to').value;
    if (from && to && to < from) { msg.innerHTML = '<div class="error">Platnosť do musí byť po platnosti od.</div>'; return; }
    var btn = form.querySelector('button[type="submit"]');
    btn.disabled = true;
    try {
      await api('/api/v1/pilots/' + pilotId + '/certificates/grant', {
        method: 'POST',
        body: JSON.stringify({
          certificateTypeCode: document.getElementById('pcg-type').value,
          externalNumber: document.getElementById('pcg-number').value.trim() || null,
          validFrom: from ? new Date(from).toISOString() : null,
          validTo: to ? new Date(to).toISOString() : null,
          mtowOrValidityText: document.getElementById('pcg-mtow').value.trim() || null
        })
      });
      if (onDone) onDone('Osvedčenie uložené.');
    } catch (err) {
      btn.disabled = false;
      msg.innerHTML = '<div class="error">' + escC(err.message || 'Uloženie zlyhalo.') + '</div>';
    }
  };
}

/** Predvyplní formulár hodnotami existujúceho osvedčenia (tlačidlo „Zmeniť“ v tabuľke). */
export function fillCertGrantForm(c) {
  if (!c || !document.getElementById('pcg-form')) return;
  var t = String(c.certificateTypeCode || '').toUpperCase().replace(/[-\/\s]/g, '_');
  if (t === 'A1A3' || t === 'A1' || t === 'A3') t = 'A1_A3';
  if (t === 'STS') t = 'STS1';
  document.getElementById('pcg-type').value = t;
  document.getElementById('pcg-number').value = c.externalNumber || '';
  document.getElementById('pcg-from').value = c.validFrom ? isoDateLocal(new Date(c.validFrom)) : '';
  document.getElementById('pcg-to').value = c.validTo ? isoDateLocal(new Date(c.validTo)) : '';
  document.getElementById('pcg-mtow').value = c.mtowOrValidityText || '';
  document.getElementById('pcg-panel').scrollIntoView({ behavior: 'smooth', block: 'center' });
  document.getElementById('pcg-number').focus();
}
