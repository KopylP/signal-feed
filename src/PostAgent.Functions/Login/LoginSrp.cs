namespace PostAgent.Login;

// The 2FA proof as Telegram defines it ("Two-factor authentication", SRP with PBKDF2-HMAC-SHA512, 100000 iterations, SHA-256),
// computed in the Mini App with WebCrypto and BigInt, so the password never leaves the owner's phone. LoginSrpTests runs this
// very script under Node and checks the proof against a Telegram-side verifier and against WTelegram's own implementation.
public static class LoginSrp
{
    // Defines srpProof(challenge, password): challenge has g (number) and p, salt1, salt2, srpB (base64);
    // it resolves to { A, M1 } in base64.
    public const string Script = """
        async function srpProof(challenge, password) {
          const subtle = globalThis.crypto.subtle;
          const fromB64 = (s) => Uint8Array.from(atob(s), (c) => c.charCodeAt(0));
          const toB64 = (b) => btoa(String.fromCharCode(...b));
          const concat = (...parts) => {
            const out = new Uint8Array(parts.reduce((n, p) => n + p.length, 0));
            let at = 0;
            for (const p of parts) { out.set(p, at); at += p.length; }
            return out;
          };
          const sha256 = async (...parts) => new Uint8Array(await subtle.digest('SHA-256', concat(...parts)));
          const big = (bytes) => bytes.length === 0 ? 0n : BigInt('0x' + Array.from(bytes, (x) => x.toString(16).padStart(2, '0')).join(''));
          const bytes256 = (n) => {
            const hex = n.toString(16).padStart(512, '0');
            return Uint8Array.from(hex.match(/../g), (h) => parseInt(h, 16));
          };
          const mod = (a, m) => ((a % m) + m) % m;
          const modPow = (base, exp, m) => {
            let result = 1n;
            base = mod(base, m);
            while (exp > 0n) {
              if (exp & 1n) result = (result * base) % m;
              base = (base * base) % m;
              exp >>= 1n;
            }
            return result;
          };

          const salt1 = fromB64(challenge.salt1);
          const salt2 = fromB64(challenge.salt2);
          const pBytes = fromB64(challenge.p);
          const bBytes = fromB64(challenge.srpB);
          const p = big(pBytes);
          const g = BigInt(challenge.g);

          // x = PH2(password, salt1, salt2)
          const ph1 = await sha256(salt2, await sha256(salt1, new TextEncoder().encode(password), salt1), salt2);
          const key = await subtle.importKey('raw', ph1, 'PBKDF2', false, ['deriveBits']);
          const pbkdf2 = new Uint8Array(await subtle.deriveBits({ name: 'PBKDF2', hash: 'SHA-512', salt: salt1, iterations: 100000 }, key, 512));
          const x = big(await sha256(salt2, pbkdf2, salt2));

          const gBytes = bytes256(g);
          const v = modPow(g, x, p);
          const k = big(await sha256(pBytes, gBytes));
          const kv = (k * v) % p;

          const a = big(globalThis.crypto.getRandomValues(new Uint8Array(256)));
          const gA = bytes256(modPow(g, a, p));
          const gB = bytes256(big(bBytes));
          const u = big(await sha256(gA, gB));
          const t = mod(big(bBytes) - kv, p);
          const kA = await sha256(bytes256(modPow(t, a + u * x, p)));

          const hp = await sha256(pBytes);
          const hg = await sha256(gBytes);
          const xored = hp.map((byte, i) => byte ^ hg[i]);
          const m1 = await sha256(xored, await sha256(salt1), await sha256(salt2), gA, gB, kA);
          return { A: toB64(gA), M1: toB64(m1) };
        }
        """;
}
