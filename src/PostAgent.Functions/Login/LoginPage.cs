namespace PostAgent.Login;

// The Mini App: one form for the code and the 2FA password. It holds no secret; what it sends is checked by LoginForm.
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
        <p>Код прийшов у застосунок акаунта-читача, у чат «Telegram». Пароль потрібен, лише якщо ввімкнено двоетапну перевірку.</p>
        <form id="form" autocomplete="off">
          <label for="code">Код з Telegram</label>
          <input id="code" inputmode="numeric" autocomplete="one-time-code" maxlength="12" placeholder="12345">
          <label for="password">Пароль двоетапної перевірки</label>
          <input id="password" type="password" autocomplete="current-password" maxlength="256">
          <button id="send" type="submit">Увійти</button>
        </form>
        <div id="status" role="status"></div>
        <script nonce="{{nonce}}">
          (function () {
            var tg = window.Telegram && window.Telegram.WebApp;
            var form = document.getElementById('form');
            var status = document.getElementById('status');
            var send = document.getElementById('send');
            var attempt = new URLSearchParams(location.search).get('a') || '';
            function say(text) { status.textContent = text; }
            if (!tg || !tg.initData || !attempt) {
              form.hidden = true;
              say('Відкрийте цю форму кнопкою «Ввести код» у чаті з ботом.');
              return;
            }
            tg.ready();
            tg.expand();
            form.addEventListener('submit', function (event) {
              event.preventDefault();
              var code = document.getElementById('code').value;
              var password = document.getElementById('password');
              if (!code.trim() && !password.value) { say('Введіть код або пароль.'); return; }
              send.disabled = true;
              say('Надсилаю…');
              fetch(location.pathname, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'omit',
                body: JSON.stringify({ attempt: attempt, initData: tg.initData, code: code, password: password.value })
              }).then(function (response) {
                password.value = '';
                if (response.status === 202) {
                  say('Надіслано. Результат прийде в чат.');
                  setTimeout(function () { tg.close(); }, 1200);
                  return;
                }
                send.disabled = false;
                say(response.status === 400 ? 'Код має бути з цифр. Перевірте його.'
                  : response.status === 409 ? 'Цю спробу завершено. Почніть знову з /login.'
                  : 'Форма застаріла. Почніть знову з /login.');
              }).catch(function () {
                send.disabled = false;
                say('Немає зв’язку. Спробуйте ще раз.');
              });
            });
          })();
        </script>
        </body>
        </html>
        """;
}
