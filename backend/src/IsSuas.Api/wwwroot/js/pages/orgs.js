import { api } from '../core/api.js';
import { navigate } from '../core/nav.js';
import { state } from '../core/state.js';
import { chipHtml, confirmAction, content, copyText, pageHead } from '../core/ui.js';

export async function renderOrgs() {
      const items = await api('/api/v1/org-units?includeInactive=true');
      const byId = {};
      items.forEach(function (o) { byId[o.id] = o; });
      const childrenMap = {};
      items.forEach(function (o) {
        var pid = o.parentId || '__root__';
        if (!childrenMap[pid]) childrenMap[pid] = [];
        childrenMap[pid].push(o);
      });
      Object.keys(childrenMap).forEach(function (k) {
        childrenMap[k].sort(function (a, b) {
          return String(a.code || '').localeCompare(String(b.code || ''), 'sk');
        });
      });

      if (!state.orgBranchFilter) state.orgBranchFilter = 'all';
      if (!state.orgCollapsed) state.orgCollapsed = {};

      function kindKey(o) {
        var k = o.kind;
        if (k === 'Root' || k === 0) return 'root';
        if (k === 'BranchPz' || k === 1) return 'pz';
        if (k === 'BranchHazz' || k === 2) return 'hazz';
        if (k === 'BranchCo' || k === 3) return 'co';
        return 'unit';
      }
      function kindLabel(o) {
        var k = kindKey(o);
        if (k === 'root') return 'MV SR';
        if (k === 'pz') return 'Policajný zbor';
        if (k === 'hazz') return 'HaZZ';
        if (k === 'co') return 'Civilná ochrana';
        return 'Útvar';
      }
      function depthLabel(o) {
        var k = kindKey(o);
        if (k !== 'unit') return kindLabel(o);
        var code = String(o.code || '');
        if (/^(KR-|HAZZ-KR-|CO-KR-)/.test(code) || /krajsk/i.test(o.name || '')) return 'Kraj';
        if (/^(OR-|HAZZ-OR-|CO-OR-)/.test(code) || /okresn/i.test(o.name || '')) return 'Okres';
        if (/útvar|stanica|pracovisko/i.test(o.name || '') || /^(UT-|HAZZ-ST-|CO-UT-)/.test(code)) return 'Útvar';
        if (o.level <= 2) return 'Kraj / útvar';
        if (o.level === 3) return 'Okres';
        return 'Útvar';
      }
      function branchOf(o) {
        var cur = o;
        var guard = 0;
        while (cur && guard++ < 20) {
          var kk = kindKey(cur);
          if (kk === 'pz' || kk === 'hazz' || kk === 'co') return kk;
          if (kk === 'root') return 'root';
          cur = cur.parentId ? byId[cur.parentId] : null;
        }
        return 'unit';
      }
      function countDesc(id) {
        var kids = childrenMap[id] || [];
        var n = kids.length;
        kids.forEach(function (c) { n += countDesc(c.id); });
        return n;
      }
      function parentOptionsHtml(excludeId) {
        return '<option value="">— koreň (bez nadradeného) —</option>' +
          items.filter(function (o) { return o.id !== excludeId && o.isActive !== false; }).map(function (o) {
            var pad = Array(Math.max(0, o.level) + 1).join('· ');
            return '<option value="' + o.id + '">' + pad + o.code + ' — ' + o.name + '</option>';
          }).join('');
      }

      function inFilter(o) {
        var f = state.orgBranchFilter;
        if (f === 'all') return true;
        var b = branchOf(o);
        if (kindKey(o) === 'root') return true;
        return b === f;
      }

      function renderNode(o) {
        if (!inFilter(o) && kindKey(o) !== 'root') return '';
        var kidsAll = childrenMap[o.id] || [];
        var kids = kidsAll;
        if (state.orgBranchFilter !== 'all') {
          kids = kidsAll.filter(function (c) {
            var b = branchOf(c);
            var ck = kindKey(c);
            return b === state.orgBranchFilter || ck === state.orgBranchFilter;
          });
        }
        var hasKids = kids.length > 0;
        var collapsed = !!state.orgCollapsed[o.id];
        if (hasKids && state.orgCollapsed[o.id] === undefined) {
          // default: collapse below kraj (level >= 2 for units under branch)
          collapsed = o.level >= 2 && kindKey(o) === 'unit';
          state.orgCollapsed[o.id] = collapsed;
        }
        var kk = kindKey(o);
        var br = branchOf(o);
        var rowClass = 'org-row'
          + (kk === 'root' ? ' is-root' : '')
          + (kk === 'pz' || kk === 'hazz' || kk === 'co' ? ' is-branch ' + kk : '')
          + (/kraj/i.test(depthLabel(o)) ? ' is-kraj' : '')
          + (o.isActive === false ? ' is-inactive' : '');
        var desc = countDesc(o.id);
        var isL2view = state.accessLevel === 2;
        var actions = isL2view
          ? ''
          : (o.isActive === false
            ? '<button type="button" class="secondary org-reactivate" data-id="' + o.id + '">Aktivovať</button>'
            : '<button type="button" class="secondary org-edit" data-id="' + o.id + '">Upraviť</button>' +
              '<button type="button" class="secondary org-remove" data-id="' + o.id +
              '" data-name="' + String(o.name).replace(/"/g, '&quot;') + '">Deaktivovať</button>');
        var html = '<li><div class="' + rowClass + '" data-id="' + o.id +
          '" data-name="' + String(o.name).replace(/"/g, '&quot;') +
          '" data-active="' + (o.isActive === false ? '0' : '1') + '">' +
          '<button type="button" class="org-toggle' + (hasKids ? '' : ' is-leaf') + '" data-toggle="' + o.id +
          '" aria-label="Rozbaliť">' + (collapsed ? '▸' : '▾') + '</button>' +
          '<div class="org-row-main">' +
          '<div class="org-row-title">' + o.name +
          (hasKids ? '<span class="org-kids">' + desc + '</span>' : '') +
          '</div>' +
          '<div class="org-row-sub">' +
          '<span class="org-code">' + o.code + '</span>' +
          '<span class="org-pill ' + (kk === 'unit' ? br : kk) + '">' + depthLabel(o) + '</span>' +
          '<span class="org-pill ' + (o.isActive === false ? 'off' : 'on') + '">' +
          (o.isActive === false ? 'Neaktívny' : 'Aktívny') + '</span>' +
          '</div></div>' +
          (actions ? '<div class="org-row-actions">' + actions + '</div>' : '') + '</div>';
        if (hasKids && !collapsed) {
          html += '<ul class="org-tree-list">' + kids.map(renderNode).join('') + '</ul>';
        }
        html += '</li>';
        return html;
      }

      var roots = (childrenMap['__root__'] || []).slice();
      // also include nodes without parent listed
      items.filter(function (o) { return !o.parentId; }).forEach(function (o) {
        if (!roots.some(function (r) { return r.id === o.id; })) roots.push(o);
      });

      function countInBranch(branch) {
        return items.filter(function (o) {
          return o.isActive !== false && branchOf(o) === branch && kindKey(o) === 'unit';
        }).length;
      }
      var inactiveCount = items.filter(function (o) { return o.isActive === false; }).length;
      var treeHtml = roots.map(renderNode).join('') || '<div class="org-empty">Zatiaľ žiadne útvary</div>';
      var isL2 = state.accessLevel === 2;
      var homeOrgForUser = null;
      if (isL2 && items.length) {
        // Najhlbší útvar v strome = domovský (MV → zložka → kraj…)
        homeOrgForUser = items.slice().sort(function (a, b) {
          return (b.level || 0) - (a.level || 0);
        })[0];
        // L2: strom vždy rozbalený
        items.forEach(function (o) { state.orgCollapsed[o.id] = false; });
        treeHtml = roots.map(renderNode).join('') || '<div class="org-empty">Zatiaľ žiadne útvary</div>';
      }

      content.innerHTML =
        '<div class="stack org-page">' +
        pageHead(
          'Organizačná štruktúra',
          isL2
            ? 'Vaša organizačná vetva (MV SR → zložka → útvar). Môžete pridávať pilotov (úroveň 3) do svojho útvaru.'
            : 'MV SR → zložky → kraj → okres → útvar. Strom je spravovateľný; filtre pomáhajú rýchlo prejsť zložky.',
          chipHtml('Uzly', items.length, 'chip-soft') +
          (isL2 ? '' : chipHtml('Neaktívne', inactiveCount)) +
          (isL2 && homeOrgForUser
            ? chipHtml('Váš útvar', homeOrgForUser.name || homeOrgForUser.code, 'chip-ok')
            : chipHtml('Úrovne', (items.reduce(function (m, o) { return Math.max(m, o.level || 0); }, 0) + 1), 'chip-ok'))
        ) +
        (isL2 ? '' :
          '<div class="org-summary">' +
          '<div class="org-summary-card pz"><div class="k">Policajný zbor</div><div class="v">' + countInBranch('pz') + '</div><div class="s">aktívne útvary v strome</div></div>' +
          '<div class="org-summary-card hazz"><div class="k">HaZZ</div><div class="v">' + countInBranch('hazz') + '</div><div class="s">aktívne útvary v strome</div></div>' +
          '<div class="org-summary-card co"><div class="k">Civilná ochrana</div><div class="v">' + countInBranch('co') + '</div><div class="s">aktívne útvary v strome</div></div>' +
          '</div>' +
          '<div class="org-branch-bar">' +
          '<button type="button" data-branch="all"' + (state.orgBranchFilter === 'all' ? ' class="is-on"' : '') + '>Celý strom</button>' +
          '<button type="button" data-branch="pz"' + (state.orgBranchFilter === 'pz' ? ' class="is-on"' : '') + '>Policajný zbor</button>' +
          '<button type="button" data-branch="hazz"' + (state.orgBranchFilter === 'hazz' ? ' class="is-on"' : '') + '>HaZZ</button>' +
          '<button type="button" data-branch="co"' + (state.orgBranchFilter === 'co' ? ' class="is-on"' : '') + '>Civilná ochrana</button>' +
          '</div>') +
        '<div class="toolbar">' +
        (isL2 ? '' : '<button type="button" id="org-add-btn">Pridať útvar</button>') +
        '<button type="button" id="org-add-user-btn">Pridať používateľa</button>' +
        (isL2 ? '' :
          '<button type="button" class="secondary" id="org-expand">Rozbaliť všetko</button>' +
          '<button type="button" class="secondary" id="org-collapse">Zbaliť kraje</button>') +
        '</div>' +
        '<div id="org-msg"></div>' +
        (state.orgUserPick && !isL2
          ? '<div class="org-pick-banner" id="org-pick-banner">' +
            '<span>Vyberte útvar v strome — kliknite na riadok (MV → zložka → kraj → okres → útvar).</span>' +
            '<button type="button" class="secondary" id="org-pick-cancel">Zrušiť výber</button></div>'
          : '') +
        '<div id="org-panel" class="form-panel hidden">' +
        '<h3 id="org-panel-title">Nový útvar</h3>' +
        '<form id="org-form" class="form-grid">' +
        '<input type="hidden" id="org-id" value="" />' +
        '<input type="hidden" id="org-rv" value="" />' +
        '<input type="hidden" id="org-orig-parent" value="" />' +
        '<label>Kód<input id="org-code" required placeholder="napr. OR-BA-1" maxlength="64" /></label>' +
        '<label>Názov<input id="org-name" required placeholder="napr. Okresné riaditeľstvo Bratislava I" maxlength="200" /></label>' +
        '<label>Typ<select id="org-kind">' +
        '<option value="4">Útvar</option>' +
        '<option value="1">Zložka PZ</option>' +
        '<option value="2">Zložka HaZZ</option>' +
        '<option value="3">Zložka CO</option>' +
        '<option value="0">Koreň (MV)</option>' +
        '</select></label>' +
        '<label class="full">Nadradený útvar<select id="org-parent">' + parentOptionsHtml(null) + '</select></label>' +
        '<div class="full toolbar">' +
        '<button type="submit">Uložiť</button>' +
        '<button type="button" class="secondary" id="org-cancel">Zrušiť</button>' +
        '</div></form></div>' +
        '<div id="org-user-panel" class="form-panel hidden">' +
        '<h3>Vytvoriť používateľa</h3>' +
        '<p class="muted">Útvar: <b id="org-user-orgname"></b>. Zadajte login — heslo sa vygeneruje automaticky.</p>' +
        '<form id="org-user-form" class="form-grid">' +
        '<input type="hidden" id="org-user-orgid" value="" />' +
        '<label>Login<input id="org-user-login" required autocomplete="off" placeholder="napr. janko.novak" /></label>' +
        '<label>Meno<input id="org-user-first" required /></label>' +
        '<label>Priezvisko<input id="org-user-last" required /></label>' +
        '<label>Email<input id="org-user-email" type="email" placeholder="voliteľné" /></label>' +
        (isL2
          ? '<input type="hidden" id="org-user-level" value="3" />' +
            '<label>Úroveň práv<input value="3 — Pilot" readonly /></label>'
          : '<label>Úroveň práv<select id="org-user-level">' +
            (state.accessLevel === 1
              ? '<option value="2">2 — Prevádzkovateľ</option><option value="3">3 — Pilot</option>'
              : '<option value="3">3 — Pilot</option>') +
            '</select></label>') +
        '<div class="full toolbar">' +
        '<button type="submit">Vytvoriť a vygenerovať heslo</button>' +
        '<button type="button" class="secondary" id="org-user-cancel">Zrušiť</button>' +
        '</div></form>' +
        '<div id="org-user-result" class="hidden"></div>' +
        '</div>' +
        '<div class="org-tree-shell' + (state.orgUserPick && !isL2 ? ' is-picking' : '') + '"><ul class="org-tree-list">' + treeHtml + '</ul></div>' +
        '</div>';

      function openOrgForm(mode, org) {
        var panel = document.getElementById('org-panel');
        var title = document.getElementById('org-panel-title');
        var codeEl = document.getElementById('org-code');
        var parentEl = document.getElementById('org-parent');
        document.getElementById('org-msg').innerHTML = '';
        if (mode === 'edit' && org) {
          title.textContent = 'Upraviť útvar';
          document.getElementById('org-id').value = org.id;
          document.getElementById('org-rv').value = String(org.rowVersion);
          document.getElementById('org-orig-parent').value = org.parentId || '';
          codeEl.value = org.code;
          codeEl.readOnly = true;
          document.getElementById('org-name').value = org.name;
          document.getElementById('org-kind').disabled = true;
          parentEl.innerHTML = parentOptionsHtml(org.id);
          parentEl.value = org.parentId || '';
        } else {
          title.textContent = 'Nový útvar';
          document.getElementById('org-id').value = '';
          document.getElementById('org-rv').value = '';
          document.getElementById('org-orig-parent').value = '';
          codeEl.value = '';
          codeEl.readOnly = false;
          document.getElementById('org-name').value = '';
          document.getElementById('org-kind').disabled = false;
          document.getElementById('org-kind').value = '4';
          parentEl.innerHTML = parentOptionsHtml(null);
          parentEl.value = '';
        }
        panel.classList.remove('hidden');
        document.getElementById('org-name').focus();
      }

      content.querySelectorAll('.org-branch-bar button').forEach(function (btn) {
        btn.onclick = function () {
          state.orgBranchFilter = btn.dataset.branch;
          navigate('orgs');
        };
      });
      var expandBtn = document.getElementById('org-expand');
      if (expandBtn) {
        expandBtn.onclick = function () {
          items.forEach(function (o) { state.orgCollapsed[o.id] = false; });
          navigate('orgs');
        };
      }
      var collapseBtn = document.getElementById('org-collapse');
      if (collapseBtn) {
        collapseBtn.onclick = function () {
          items.forEach(function (o) {
            state.orgCollapsed[o.id] = o.level >= 2 && kindKey(o) === 'unit';
          });
          navigate('orgs');
        };
      }
      content.querySelectorAll('[data-toggle]').forEach(function (btn) {
        btn.onclick = function () {
          var id = btn.dataset.toggle;
          state.orgCollapsed[id] = !state.orgCollapsed[id];
          navigate('orgs');
        };
      });

      var addOrgBtn = document.getElementById('org-add-btn');
      if (addOrgBtn) {
        addOrgBtn.onclick = function () {
          state.orgUserPick = false;
          openOrgForm('create');
        };
      }
      var orgCancel = document.getElementById('org-cancel');
      if (orgCancel) {
        orgCancel.onclick = function () {
          document.getElementById('org-panel').classList.add('hidden');
        };
      }

      function openUserFormForOrg(orgId, orgName) {
        document.getElementById('org-panel').classList.add('hidden');
        var panel = document.getElementById('org-user-panel');
        document.getElementById('org-user-orgid').value = orgId;
        document.getElementById('org-user-orgname').textContent = orgName || '';
        document.getElementById('org-user-login').value = '';
        document.getElementById('org-user-first').value = '';
        document.getElementById('org-user-last').value = '';
        document.getElementById('org-user-email').value = '';
        var levelEl = document.getElementById('org-user-level');
        if (levelEl && state.accessLevel === 2) levelEl.value = '3';
        document.getElementById('org-user-result').classList.add('hidden');
        document.getElementById('org-user-result').innerHTML = '';
        panel.classList.remove('hidden');
        panel.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        document.getElementById('org-user-login').focus();
      }

      document.getElementById('org-add-user-btn').onclick = function () {
        document.getElementById('org-panel').classList.add('hidden');
        if (isL2) {
          state.orgUserPick = false;
          if (!homeOrgForUser) {
            document.getElementById('org-msg').innerHTML =
              '<div class="error">Nemáte priradený útvar na vytvorenie používateľa.</div>';
            return;
          }
          openUserFormForOrg(homeOrgForUser.id, homeOrgForUser.name || homeOrgForUser.code);
          return;
        }
        document.getElementById('org-user-panel').classList.add('hidden');
        state.orgUserPick = true;
        state.orgPendingUserOrgId = null;
        items.forEach(function (o) { state.orgCollapsed[o.id] = false; });
        navigate('orgs');
      };
      var pickCancel = document.getElementById('org-pick-cancel');
      if (pickCancel) {
        pickCancel.onclick = function () {
          state.orgUserPick = false;
          state.orgPendingUserOrgId = null;
          navigate('orgs');
        };
      }

      content.querySelectorAll('.org-edit').forEach(function (btn) {
        btn.onclick = function (ev) {
          ev.stopPropagation();
          var org = byId[btn.dataset.id];
          if (org) openOrgForm('edit', org);
        };
      });

      if (state.orgUserPick && !isL2) {
        content.querySelectorAll('.org-row').forEach(function (row) {
          row.onclick = function (ev) {
            if (ev.target.closest('.org-toggle') || ev.target.closest('.org-row-actions')) return;
            if (row.dataset.active === '0') {
              document.getElementById('org-msg').innerHTML =
                '<div class="error">Vyberte aktívny útvar.</div>';
              return;
            }
            state.orgUserPick = false;
            state.orgPendingUserOrgId = row.dataset.id;
            state.orgPendingUserOrgName = row.dataset.name || '';
            navigate('orgs');
          };
        });
      } else if (state.orgPendingUserOrgId && !isL2) {
        var pendingId = state.orgPendingUserOrgId;
        var pendingName = state.orgPendingUserOrgName;
        state.orgPendingUserOrgId = null;
        state.orgPendingUserOrgName = null;
        openUserFormForOrg(pendingId, pendingName);
      }

      document.getElementById('org-user-cancel').onclick = function () {
        document.getElementById('org-user-panel').classList.add('hidden');
        state.orgUserPick = false;
        state.orgPendingUserOrgId = null;
      };
      document.getElementById('org-user-form').onsubmit = async function (ev) {
        ev.preventDefault();
        var msg = document.getElementById('org-msg');
        var result = document.getElementById('org-user-result');
        try {
          var created = await api('/api/v1/accounts', {
            method: 'POST',
            body: JSON.stringify({
              userName: document.getElementById('org-user-login').value.trim(),
              firstName: document.getElementById('org-user-first').value.trim(),
              lastName: document.getElementById('org-user-last').value.trim(),
              email: document.getElementById('org-user-email').value.trim() || null,
              homeOrgUnitId: document.getElementById('org-user-orgid').value,
              accessLevel: Number(document.getElementById('org-user-level').value)
            })
          });
          result.classList.remove('hidden');
          var createdPw = created.plainPassword || '';
          result.innerHTML = '<div class="msg"><b>Účet vytvorený.</b><br/>Login: <code>' +
            created.account.userName + '</code><br/>Heslo (uložte si ho teraz): <code id="org-gen-pw">' +
            createdPw + '</code> ' +
            '<button type="button" class="secondary" id="org-copy-pw">Kopírovať heslo</button></div>' +
            (Number(document.getElementById('org-user-level').value) === 3
              ? '<p class="muted">Osvedčenie prideliť v menu <b>Osvedčenia</b>.</p>'
              : '');
          var copyBtn = document.getElementById('org-copy-pw');
          if (copyBtn) {
            copyBtn.onclick = function () {
              copyText(createdPw).then(function () {
                copyBtn.textContent = 'Skopírované';
                setTimeout(function () { copyBtn.textContent = 'Kopírovať heslo'; }, 1800);
              }).catch(function () {
                copyBtn.textContent = 'Nepodarilo sa skopírovať';
                setTimeout(function () { copyBtn.textContent = 'Kopírovať heslo'; }, 1800);
              });
            };
          }
          msg.innerHTML = '';
        } catch (err) {
          msg.innerHTML = '<div class="error">' + err.message + '</div>';
        }
      };

      content.querySelectorAll('.org-reactivate').forEach(function (btn) {
        btn.onclick = async function (ev) {
          ev.stopPropagation();
          try {
            await api('/api/v1/org-units/' + btn.dataset.id + '/reactivate', { method: 'POST', body: '{}' });
            navigate('orgs');
          } catch (err) {
            document.getElementById('org-msg').innerHTML = '<div class="error">' + err.message + '</div>';
          }
        };
      });
      document.getElementById('org-form').onsubmit = async function (ev) {
        ev.preventDefault();
        var msg = document.getElementById('org-msg');
        var id = document.getElementById('org-id').value;
        var name = document.getElementById('org-name').value.trim();
        var parentVal = document.getElementById('org-parent').value || null;
        try {
          if (!name) throw new Error('Vyplňte názov');
          if (!id) {
            var code = document.getElementById('org-code').value.trim();
            if (!code) throw new Error('Vyplňte kód a názov');
            var kind = Number(document.getElementById('org-kind').value);
            await api('/api/v1/org-units', {
              method: 'POST',
              body: JSON.stringify({ code: code, name: name, parentId: parentVal, kind: kind })
            });
          } else {
            var rv = Number(document.getElementById('org-rv').value);
            var origParent = document.getElementById('org-orig-parent').value || null;
            var updated = await api('/api/v1/org-units/' + id, {
              method: 'PATCH',
              body: JSON.stringify({ name: name, rowVersion: rv })
            });
            if ((parentVal || null) !== (origParent || null)) {
              await api('/api/v1/org-units/' + id + '/move', {
                method: 'POST',
                body: JSON.stringify({
                  newParentId: parentVal,
                  reason: 'Úprava z UI',
                  rowVersion: updated.rowVersion
                })
              });
            }
          }
          navigate('orgs');
        } catch (err) {
          msg.innerHTML = '<div class="error">' + (err.message || 'Uloženie zlyhalo') + '</div>';
        }
      };
      content.querySelectorAll('.org-remove').forEach(function (btn) {
        btn.onclick = async function (ev) {
          ev.stopPropagation();
          if (!confirmAction('Naozaj deaktivovať útvar „' + (btn.dataset.name || '') + '“?')) return;
          try {
            await api('/api/v1/org-units/' + btn.dataset.id, { method: 'DELETE' });
            navigate('orgs');
          } catch (err) {
            document.getElementById('org-msg').innerHTML = '<div class="error">' + (err.message || 'Deaktivácia zlyhala') + '</div>';
          }
        };
      });
      return;
}
