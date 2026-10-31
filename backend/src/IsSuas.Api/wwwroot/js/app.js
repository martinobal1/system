import { api, setMaintenanceHandler, setUnauthorizedHandler } from './core/api.js';
import { checkMaintenanceOnStart, initMaintenance, showMaintenanceScreen } from './core/maintenance.js';
import { initTrashButtons } from './core/trash.js';
import { initPasswordToggles } from './core/passwordToggle.js';
import { applyDetailFromPath, applyNavByRole, closeNav, defaultPageForRole, initNav, isPageAllowedForRole, navigate, pageFromPath } from './core/nav.js';
import { applyLoginSession, clearSession, hasUsableSession, state } from './core/state.js';
import { appView, displayNameEl, loginBtn, loginError, loginView } from './core/ui.js';
    // Stranka vyziadana v URL pred prihlasenim; otvori sa az po uspesnom logine.
    var pendingRoute = null;


    function showError(msg) {
      loginError.textContent = msg;
      loginError.classList.remove('hidden');
    }

    setUnauthorizedHandler(function () { logout(true); });
    setMaintenanceHandler(function (status) { showMaintenanceScreen(status); });
    initMaintenance();
    initTrashButtons();
    initPasswordToggles();

    function showApp() {
      document.documentElement.classList.add('issuas-has-session');
      loginView.classList.add('hidden');
      appView.classList.remove('hidden');
      displayNameEl.textContent = state.displayName;
      var avatar = document.getElementById('user-avatar');
      if (avatar) {
        var parts = String(state.displayName || 'U').trim().split(/\s+/).filter(Boolean);
        var initials = parts.length >= 2
          ? (parts[0][0] + parts[1][0])
          : String(parts[0] || 'U').slice(0, 2);
        avatar.textContent = initials.toUpperCase();
      }
      var roleNames = { 1: 'Gestor', 2: 'Prevádzkovateľ', 3: 'Pilot' };
      var roleEl = document.getElementById('user-role');
      if (roleEl) roleEl.textContent = roleNames[state.accessLevel] || '';
      var ddName = document.getElementById('dropdown-name');
      if (ddName) ddName.textContent = state.displayName || '';
      var ddUser = document.getElementById('dropdown-user');
      if (ddUser) ddUser.textContent = state.userName ? '@' + state.userName : '';
      applyNavByRole();
      var requested = pendingRoute;
      pendingRoute = null;
      var page = requested && isPageAllowedForRole(requested) ? requested : defaultPageForRole();
      navigate(page, { replaceUrl: true });
    }

    function showLogin() {
      document.documentElement.classList.remove('issuas-has-session');
      closeNav();
      if (window.location.pathname !== '/') window.history.replaceState({}, '', '/');
      appView.classList.add('hidden');
      loginView.classList.remove('hidden');
      var loginForm = document.getElementById('login-form');
      if (loginForm) loginForm.reset();
      document.getElementById('username').value = '';
      document.getElementById('password').value = '';
    }

    function logout(clearStorage = true) {
      clearSession(clearStorage);
      showLogin();
    }

    document.getElementById('login-form').addEventListener('submit', async function (e) {
      e.preventDefault();
      loginError.classList.add('hidden');
      loginBtn.disabled = true;
      loginBtn.textContent = 'Prihlasujem…';
      try {
        const body = {
          userName: document.getElementById('username').value.trim(),
          password: document.getElementById('password').value
        };
        const res = await api('/api/v1/auth/login', {
          method: 'POST',
          body: JSON.stringify(body)
        });
        applyLoginSession(res);
        showApp();
      } catch (err) {
        showError(err.message || 'Prihlasenie zlyhalo');
      } finally {
        loginBtn.disabled = false;
        loginBtn.textContent = 'Prihlásiť sa';
      }
    });

    async function completeSsoCallbackFromUrl() {
      var path = window.location.pathname || '';
      if (path.indexOf('/auth/callback') < 0) return false;
      var params = new URLSearchParams(window.location.search);
      var code = params.get('code');
      if (!code) {
        showError('Spätné volanie prihlásenia: chýba kód');
        return true;
      }
      try {
        var q = '/api/v1/auth/sso/callback?code=' + encodeURIComponent(code);
        var stateParam = params.get('state');
        if (stateParam) q += '&state=' + encodeURIComponent(stateParam);
        var res = await api(q);
        applyLoginSession(res);
        // Odkial prihlasenie zacalo (deep link), tam sa po logine vratime.
        var next = params.get('next');
        if (next) {
          pendingRoute = pageFromPath(next);
          applyDetailFromPath(next);
        }
        window.history.replaceState({}, '', '/');
        showApp();
      } catch (err) {
        showError(err.message || 'Spätné volanie prihlásenia zlyhalo');
      }
      return true;
    }

    document.getElementById('logout-btn').addEventListener('click', function () {
      logout(true);
    });

    var passwordDialog = document.getElementById('password-dialog');
    var passwordForm = document.getElementById('password-form');
    var passwordError = document.getElementById('pw-error');
    var passwordOk = document.getElementById('pw-ok');
    var passwordSave = document.getElementById('pw-save');
    var pwCurrent = document.getElementById('pw-current');
    var pwNew = document.getElementById('pw-new');
    var pwConfirm = document.getElementById('pw-confirm');

    function setSlovakPasswordValidity(el) {
      el.setCustomValidity('');
      if (el.validity.valueMissing) {
        el.setCustomValidity('Toto pole je povinné.');
        return;
      }
      if (el.validity.tooShort) {
        el.setCustomValidity('Nové heslo musí mať aspoň 8 znakov (teraz má ' + el.value.length + ').');
        return;
      }
      if (el === pwConfirm && el.value !== pwNew.value) {
        el.setCustomValidity('Nové heslo a potvrdenie sa nezhodujú.');
      }
    }

    [pwCurrent, pwNew, pwConfirm].forEach(function (el) {
      el.addEventListener('invalid', function () { setSlovakPasswordValidity(el); });
      el.addEventListener('input', function () {
        setSlovakPasswordValidity(el);
        var liveError = passwordFormClientError();
        if (liveError && !passwordError.classList.contains('hidden')) {
          showPasswordFormError(liveError);
        } else if (!liveError) {
          passwordError.classList.add('hidden');
        }
      });
    });

    function setPasswordDialogOpen(open) {
      passwordDialog.classList.toggle('hidden', !open);
      if (!open) return;
      passwordError.classList.add('hidden');
      passwordOk.classList.add('hidden');
      passwordForm.reset();
      setTimeout(function () {
        var first = document.getElementById('pw-current');
        if (first) first.focus();
      }, 0);
    }

    document.getElementById('change-password-btn').addEventListener('click', function () {
      setPasswordDialogOpen(true);
    });
    document.getElementById('pw-cancel').addEventListener('click', function () {
      setPasswordDialogOpen(false);
    });
    passwordDialog.addEventListener('click', function (ev) {
      if (ev.target === passwordDialog) setPasswordDialogOpen(false);
    });

    function showPasswordFormError(msg) {
      passwordOk.classList.add('hidden');
      passwordError.textContent = msg;
      passwordError.classList.remove('hidden');
    }

    function passwordFormClientError() {
      if (!pwCurrent.value) return 'Zadajte aktuálne heslo.';
      if (!pwNew.value) return 'Zadajte nové heslo.';
      if (pwNew.value.length < 8) {
        return 'Nové heslo musí mať aspoň 8 znakov (teraz má ' + pwNew.value.length + ').';
      }
      if (!pwConfirm.value) return 'Zadajte nové heslo znova.';
      if (pwConfirm.value.length < 8) {
        return 'Potvrdenie hesla musí mať aspoň 8 znakov (teraz má ' + pwConfirm.value.length + ').';
      }
      if (pwNew.value !== pwConfirm.value) return 'Nové heslo a potvrdenie sa nezhodujú.';
      return '';
    }

    passwordForm.addEventListener('submit', async function (e) {
      e.preventDefault();
      passwordError.classList.add('hidden');
      passwordOk.classList.add('hidden');
      var clientError = passwordFormClientError();
      if (clientError) {
        showPasswordFormError(clientError);
        return;
      }
      var currentPassword = pwCurrent.value;
      var newPassword = pwNew.value;
      var confirmPassword = pwConfirm.value;
      passwordSave.disabled = true;
      try {
        await api('/api/v1/auth/change-password', {
          method: 'POST',
          body: JSON.stringify({
            currentPassword: currentPassword,
            newPassword: newPassword,
            confirmNewPassword: confirmPassword
          })
        });
        passwordOk.classList.remove('hidden');
        passwordForm.reset();
      } catch (err) {
        passwordError.textContent = err.message || 'Zmena hesla zlyhala';
        passwordError.classList.remove('hidden');
      } finally {
        passwordSave.disabled = false;
      }
    });

    document.addEventListener('keydown', function (ev) {
      if (ev.key !== 'Escape') return;
      if (!passwordDialog.classList.contains('hidden')) {
        setPasswordDialogOpen(false);
        return;
      }
      closeNav();
    });

    initNav();

    pendingRoute = pageFromPath(window.location.pathname);
    applyDetailFromPath(window.location.pathname);
    if (pendingRoute === 'search') {
      state.searchQuery = new URLSearchParams(window.location.search).get('q') || '';
    }

    var isSsoCallback = (window.location.pathname || '').indexOf('/auth/callback') >= 0;
    if (!isSsoCallback && state.token && hasUsableSession()) {
      showApp();
    } else {
      completeSsoCallbackFromUrl().then(function (handled) {
        if (handled) return;
        if (state.token && hasUsableSession()) showApp();
        else showLogin();
      });
    }

    checkMaintenanceOnStart();
