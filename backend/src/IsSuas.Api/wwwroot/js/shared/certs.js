import { state } from '../core/state.js';
import { api } from '../core/api.js';
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
