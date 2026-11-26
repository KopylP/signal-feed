namespace PostAgent.Login;

// The Mini App: a form for the phone (stage "phone"), the code ("code") or the 2FA password ("password").
// The password is turned into an SRP proof on the phone (LoginSrp) and only the proof is sent. It holds no secret; what it sends is checked by LoginForm.
public static class LoginPage
{
    public const string TelegramScript = "https://telegram.org/js/telegram-web-app.js";

    // Only our inline script (by nonce) and Telegram's script run, the page talks only to its own origin, and only Telegram Web may frame it.
    public static string ContentSecurityPolicy(string nonce) =>
        $"default-src 'none'; script-src 'nonce-{nonce}' {TelegramScript}; style-src 'unsafe-inline'; connect-src 'self'; "
        + "img-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors https://web.telegram.org";

    public static string Html(string nonce) => $$"""
        <!doctype html>
        <html lang="uk">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="referrer" content="no-referrer">
        <title>Вхід у Telegram</title>
        <script nonce="{{nonce}}" src="{{TelegramScript}}"></script>
        <style>
          body { font-family: system-ui, sans-serif; margin: 0; padding: 20px 16px; background: var(--tg-theme-bg-color, #fff); color: var(--tg-theme-text-color, #111); }
          h1 { font-size: 20px; margin: 0 0 8px; }
          p { color: var(--tg-theme-hint-color, #666); font-size: 14px; margin: 0 0 16px; }
          label { display: block; font-size: 14px; margin: 12px 0 4px; }
          input { box-sizing: border-box; width: 100%; font-size: 20px; padding: 10px 12px; border-radius: 8px; border: 1px solid var(--tg-theme-hint-color, #ccc);
                  background: var(--tg-theme-secondary-bg-color, #f4f4f5); color: inherit; }
          #code { letter-spacing: 6px; text-align: center; }
          button { width: 100%; margin-top: 20px; font-size: 17px; padding: 12px; border: 0; border-radius: 8px;
                   background: var(--tg-theme-button-color, #2481cc); color: var(--tg-theme-button-text-color, #fff); }
          button:disabled { opacity: .6; }
          #status { margin-top: 16px; font-size: 15px; min-height: 20px; }
        </style>
        </head>
        <body>
        <h1>Вхід акаунта-читача</h1>
        <p id="hint-phone" hidden>Номер телефону акаунта, яким бот читатиме канали. Telegram надішле на нього код.</p>
        <p id="hint-code" hidden>Код прийшов у застосунок акаунта-читача, у чат «Telegram».</p>
        <p id="hint-password" hidden>Пароль двоетапної перевірки акаунта-читача. Форма перевіряє його тут, на телефоні: сам пароль нікуди не надсилається і ніде не зберігається.</p>
        <form id="form" autocomplete="off">
          <div id="phone-step" hidden>
            <label for="phone">Номер телефону</label>
            <input id="phone" type="tel" inputmode="tel" autocomplete="tel" maxlength="24" placeholder="+380…">
          </div>
          <div id="code-step" hidden>
            <label for="code">Код з Telegram</label>
            <input id="code" inputmode="numeric" autocomplete="one-time-code" maxlength="12" placeholder="12345">
          </div>
          <div id="password-step" hidden>
            <label for="password">Пароль двоетапної перевірки</label>
            <input id="password" type="password" autocomplete="current-password" maxlength="256">
          </div>
          <button id="send" type="submit">Далі</button>
        </form>
        <div id="status" role="status"></div>
        <script nonce="{{nonce}}">
        {{LoginSrp.Script}}
          (function () {
            var tg = window.Telegram && window.Telegram.WebApp;
            var form = document.getElementById('form');
            var status = document.getElementById('status');
            var send = document.getElementById('send');
            var query = new URLSearchParams(location.search);
            var attempt = query.get('a') || '';
            var stage = ['phone', 'code', 'password'].indexOf(query.get('s')) >= 0 ? query.get('s') : 'phone';
            var challenge = null;
            function say(text) { status.textContent = text; }
            document.getElementById(stage + '-step').hidden = false;
            document.getElementById('hint-' + stage).hidden = false;
            if (!tg || !tg.initData || !attempt) {
              form.hidden = true;
              say('Відкрийте цю форму кнопкою в чаті з ботом.');
              return;
            }
            tg.ready();
            tg.expand();
            function post(payload) {
              payload.attempt = attempt;
              payload.initData = tg.initData;
              return fetch(location.pathname, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'omit',
                body: JSON.stringify(payload)
              });
            }
            function failed(status) {
              send.disabled = false;
              say(status === 400 ? (stage === 'phone' ? 'Перевірте номер: +, код країни і цифри.' : 'Код має бути з цифр. Перевірте його.')
                : status === 409 ? 'Цей крок уже неактуальний. Скористайтеся найновішою кнопкою в чаті або почніть знову з /login.'
                : 'Форма застаріла. Почніть знову з /login.');
            }
            if (stage === 'password') {
              send.disabled = true;
              say('Готую перевірку…');
              post({ action: 'challenge' }).then(function (response) {
                if (response.status !== 200) { failed(response.status); return null; }
                return response.json();
              }).then(function (data) {
                if (data) { challenge = data; send.disabled = false; say(''); }
              }).catch(function () { send.disabled = false; say('Немає зв’язку. Спробуйте ще раз.'); });
            }
            form.addEventListener('submit', function (event) {
              event.preventDefault();
              var phone = document.getElementById('phone').value;
              var code = document.getElementById('code').value;
              var password = document.getElementById('password');
              if (stage === 'phone' && !phone.trim()) { say('Введіть номер.'); return; }
              if (stage === 'code' && !code.trim()) { say('Введіть код.'); return; }
              if (stage === 'password' && (!password.value || !challenge)) { say('Введіть пароль.'); return; }
              send.disabled = true;
              say(stage === 'password' ? 'Перевіряю…' : 'Надсилаю…');
              var payload = stage === 'phone' ? Promise.resolve({ phone: phone })
                : stage === 'code' ? Promise.resolve({ code: code })
                : srpProof(challenge, password.value).then(function (proof) { return { srpA: proof.A, srpM1: proof.M1 }; });
              payload.then(function (body) {
                password.value = '';
                return post(body);
              }).then(function (response) {
                if (response.status === 202) {
                  say(stage === 'phone' ? 'Номер надіслано. Кнопка для коду прийде в чат.' : 'Надіслано. Результат прийде в чат.');
                  setTimeout(function () { tg.close(); }, 1200);
                  return;
                }
                failed(response.status);
              }).catch(function () {
                password.value = '';
                send.disabled = false;
                say('Не вдалося. Спробуйте ще раз.');
              });
            });
          })();
        </script>
        </body>
        </html>
        """;
}
