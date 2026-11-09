// =====================================================================================
// Cofre do insiTalent: proteção dos dados pessoais no navegador.
//
// • Modo SIGILOSO (padrão): os dados ficam só na memória desta aba. Nada é gravado
//   no navegador; ao fechar ou atualizar a aba, tudo é descartado.
// • Modo PROTEGIDO: os dados são gravados no navegador criptografados (AES-GCM 256).
//   A chave dos dados é aleatória e fica guardada cifrada por uma chave derivada da
//   senha (PBKDF2-SHA256). Sem a senha, ninguém lê os dados — nem quem usar o mesmo
//   computador. A senha é pedida ao abrir e de novo após 30 minutos sem uso.
// • Preferências visuais (tema, papel de parede, configurações) não são dados pessoais
//   e continuam salvas normalmente.
// =====================================================================================
(function () {
    const I = window.insimatch;
    const ls = window.localStorage;
    const PREFIXO_CIFRADO = 'insi.c:';
    const CHAVE_COFRE = 'insitalent.cofre';
    const CHAVE_MODO = 'insitalent.modoDados';
    const INATIVIDADE_MS = 30 * 60 * 1000;
    const ITERACOES = 310000;

    // Chaves que NÃO são dados pessoais (ficam sempre salvas, sem criptografia)
    const PREFS_EXATAS = ['insimatch.tema', 'insitalent.fundo', 'insitalent.movimento', 'insitalent.config',
        'insitalent.gemini', 'insitalent.aba', 'insimatch.visual', CHAVE_COFRE, CHAVE_MODO];
    const PREFS_PREFIXOS = ['insitalent.modelo.'];
    // Chaves de dados pessoais que versões antigas gravavam sem proteção
    const DADOS_EXATOS = ['insimatch.colaboradores', 'insitalent.abas', 'insitalent.clientes', 'insitalent.times'];
    const DADOS_PREFIXOS = ['insitalent.dados.'];

    const ehPref = k => PREFS_EXATAS.includes(k) || PREFS_PREFIXOS.some(p => k.startsWith(p));
    const ehDadoAntigo = k => DADOS_EXATOS.includes(k) || DADOS_PREFIXOS.some(p => k.startsWith(p));

    const estado = {
        chave: null,            // CryptoKey dos dados (só na memória, só enquanto desbloqueado)
        mem: new Map(),         // modo sigiloso: textos
        memImg: new Map(),      // modo sigiloso: imagens
        cache: new Map(),       // modo protegido: textos já decifrados
        ultimoUso: Date.now(),
        ref: null               // componente .NET avisado quando o cofre bloqueia
    };

    const modo = () => (ls.getItem(CHAVE_MODO) === 'protegido' && ls.getItem(CHAVE_COFRE)) ? 'protegido' : 'sigiloso';

    // ---------- utilitários de criptografia ----------
    const enc = new TextEncoder(), dec = new TextDecoder();
    const b64 = buf => { let s = ''; const b = new Uint8Array(buf); for (let i = 0; i < b.length; i += 0x8000) s += String.fromCharCode.apply(null, b.subarray(i, i + 0x8000)); return btoa(s); };
    const deB64 = s => Uint8Array.from(atob(s), c => c.charCodeAt(0));

    async function derivar(senha, sal, iteracoes) {
        const base = await crypto.subtle.importKey('raw', enc.encode(senha), 'PBKDF2', false, ['deriveKey']);
        return crypto.subtle.deriveKey({ name: 'PBKDF2', salt: sal, iterations: iteracoes, hash: 'SHA-256' },
            base, { name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt']);
    }
    async function cifrar(chave, texto) {
        const iv = crypto.getRandomValues(new Uint8Array(12));
        const c = await crypto.subtle.encrypt({ name: 'AES-GCM', iv }, chave, typeof texto === 'string' ? enc.encode(texto) : texto);
        return b64(iv) + '.' + b64(c);
    }
    async function decifrarBytes(chave, pacote) {
        const [iv, c] = pacote.split('.');
        return new Uint8Array(await crypto.subtle.decrypt({ name: 'AES-GCM', iv: deB64(iv) }, chave, deB64(c)));
    }
    const decifrar = async (chave, pacote) => dec.decode(await decifrarBytes(chave, pacote));

    async function novoEnvelope(senha, dadosChave) {
        const sal = crypto.getRandomValues(new Uint8Array(16));
        const kek = await derivar(senha, sal, ITERACOES);
        return JSON.stringify({ v: 1, sal: b64(sal), iter: ITERACOES, chave: await cifrar(kek, dadosChave) });
    }
    async function abrirEnvelope(senha) {
        const env = JSON.parse(ls.getItem(CHAVE_COFRE));
        const kek = await derivar(senha, deB64(env.sal), env.iter);
        const bruta = await decifrarBytes(kek, env.chave);        // senha errada → erro aqui
        return { bruta, chave: await crypto.subtle.importKey('raw', bruta, 'AES-GCM', false, ['encrypt', 'decrypt']) };
    }

    // ---------- IndexedDB (imagens) ----------
    async function idb(modoTx, fn) {
        const db = await I._abrirDb();
        return new Promise((ok, erro) => {
            const tx = db.transaction('imagens', modoTx);
            const r = fn(tx.objectStore('imagens'));
            tx.oncomplete = () => ok(r && 'result' in r ? r.result : undefined);
            tx.onerror = () => erro(tx.error);
        });
    }
    const idbGet = id => idb('readonly', s => s.get(id));
    const idbPut = (id, v) => idb('readwrite', s => s.put(v, id));
    const idbDel = id => idb('readwrite', s => s.delete(id));
    const idbChaves = () => idb('readonly', s => s.getAllKeys());
    const idbLimpar = () => idb('readwrite', s => s.clear());

    // ---------- leitura/gravação usadas por todo o sistema ----------
    I.ler = async function (k) {
        try {
            if (ehPref(k)) return ls.getItem(k);
            if (modo() === 'sigiloso') return estado.mem.has(k) ? estado.mem.get(k) : null;
            if (!estado.chave) return null;
            if (estado.cache.has(k)) return estado.cache.get(k);
            const pacote = ls.getItem(PREFIXO_CIFRADO + k);
            if (!pacote) return null;
            const t = await decifrar(estado.chave, pacote);
            estado.cache.set(k, t);
            return t;
        } catch (e) { return null; }
    };
    I.gravar = async function (k, v) {
        try {
            if (ehPref(k)) { ls.setItem(k, v); return; }
            if (modo() === 'sigiloso') { estado.mem.set(k, v); return; }
            if (!estado.chave) return;            // bloqueado: não grava
            estado.cache.set(k, v);
            ls.setItem(PREFIXO_CIFRADO + k, await cifrar(estado.chave, v));
        } catch (e) { console.warn('insiTalent: não foi possível gravar', e); }
    };
    I.remover = function (k) {
        try {
            estado.mem.delete(k); estado.cache.delete(k);
            ls.removeItem(k); ls.removeItem(PREFIXO_CIFRADO + k);
        } catch (e) { }
    };

    I.imgSalvar = async function (id, dataUrl) {
        if (modo() === 'sigiloso') { estado.memImg.set(id, dataUrl); return true; }
        if (!estado.chave) return false;
        await idbPut(id, 'enc:' + await cifrar(estado.chave, dataUrl));
        return true;
    };
    I.imgLer = async function (ids) {
        const r = {};
        for (const id of ids) {
            try {
                if (modo() === 'sigiloso') { if (estado.memImg.has(id)) r[id] = estado.memImg.get(id); continue; }
                if (!estado.chave) continue;
                const v = await idbGet(id);
                if (typeof v === 'string' && v.startsWith('enc:')) r[id] = await decifrar(estado.chave, v.slice(4));
            } catch (e) { }
        }
        return r;
    };
    I.imgRemover = async function (id) {
        estado.memImg.delete(id);
        try { await idbDel(id); } catch (e) { }
    };

    // ---------- inatividade: bloqueia após 30 minutos sem uso ----------
    const marcarUso = () => { estado.ultimoUso = Date.now(); };
    ['pointerdown', 'keydown', 'wheel', 'touchstart', 'mousemove'].forEach(ev =>
        document.addEventListener(ev, marcarUso, { passive: true, capture: true }));
    function verificar() {
        if (modo() === 'protegido' && estado.chave && Date.now() - estado.ultimoUso > INATIVIDADE_MS) I.cofreBloquear();
    }
    setInterval(verificar, 15000);
    document.addEventListener('visibilitychange', () => { if (!document.hidden) verificar(); });

    // ---------- dados antigos sem proteção ----------
    async function temImagensAbertas() {
        try {
            for (const id of await idbChaves()) {
                const v = await idbGet(id);
                if (typeof v === 'string' && !v.startsWith('enc:')) return true;
            }
        } catch (e) { }
        return false;
    }
    const chavesAntigas = () => Object.keys(ls).filter(ehDadoAntigo);

    // ---------- API do cofre ----------
    I.cofreRegistrar = ref => { estado.ref = ref; };

    I.cofreEstado = async function () {
        const m = modo();
        const legado = chavesAntigas().length > 0 || (m === 'sigiloso' && await temImagensAbertas());
        return { modo: m, desbloqueado: m === 'sigiloso' || !!estado.chave, legado };
    };

    // Liga o modo protegido (cria a senha) e cifra tudo o que está na memória e o que estava aberto
    I.cofreCriar = async function (senha) {
        const bruta = crypto.getRandomValues(new Uint8Array(32));
        const chave = await crypto.subtle.importKey('raw', bruta, 'AES-GCM', false, ['encrypt', 'decrypt']);
        ls.setItem(CHAVE_COFRE, await novoEnvelope(senha, bruta));
        estado.chave = chave;
        ls.setItem(CHAVE_MODO, 'protegido');
        // memória → cofre
        for (const [k, v] of estado.mem) { estado.cache.set(k, v); ls.setItem(PREFIXO_CIFRADO + k, await cifrar(chave, v)); }
        for (const [id, v] of estado.memImg) await idbPut(id, 'enc:' + await cifrar(chave, v));
        estado.mem.clear(); estado.memImg.clear();
        // dados antigos abertos → cofre (e apaga a versão aberta)
        for (const k of chavesAntigas()) {
            const v = ls.getItem(k);
            if (v !== null && !ls.getItem(PREFIXO_CIFRADO + k)) ls.setItem(PREFIXO_CIFRADO + k, await cifrar(chave, v));
            ls.removeItem(k);
        }
        try {
            for (const id of await idbChaves()) {
                const v = await idbGet(id);
                if (typeof v === 'string' && !v.startsWith('enc:')) await idbPut(id, 'enc:' + await cifrar(chave, v));
            }
        } catch (e) { }
        marcarUso();
        return true;
    };

    I.cofreAbrir = async function (senha) {
        try {
            const { chave } = await abrirEnvelope(senha);
            estado.chave = chave;
            estado.cache.clear();
            marcarUso();
            return true;
        } catch (e) { return false; }
    };

    I.cofreBloquear = function () {
        if (modo() !== 'protegido') return;
        estado.chave = null;
        estado.cache.clear();
        if (estado.ref) estado.ref.invokeMethodAsync('CofreBloqueado').catch(() => { });
    };

    I.cofreTrocarSenha = async function (atual, nova) {
        try {
            const { bruta } = await abrirEnvelope(atual);
            ls.setItem(CHAVE_COFRE, await novoEnvelope(nova, bruta));
            return true;
        } catch (e) { return false; }
    };

    // Volta ao modo sigiloso: os dados continuam abertos nesta aba (memória) e o que estava salvo é apagado
    I.cofreDesativar = async function () {
        if (modo() !== 'protegido' || !estado.chave) return false;
        for (const k of Object.keys(ls).filter(k => k.startsWith(PREFIXO_CIFRADO))) {
            try { estado.mem.set(k.slice(PREFIXO_CIFRADO.length), await decifrar(estado.chave, ls.getItem(k))); } catch (e) { }
            ls.removeItem(k);
        }
        try {
            for (const id of await idbChaves()) {
                const v = await idbGet(id);
                if (typeof v === 'string' && v.startsWith('enc:')) {
                    try { estado.memImg.set(id, await decifrar(estado.chave, v.slice(4))); } catch (e) { }
                }
            }
            await idbLimpar();
        } catch (e) { }
        ls.removeItem(CHAVE_COFRE);
        ls.setItem(CHAVE_MODO, 'sigiloso');
        estado.chave = null; estado.cache.clear();
        return true;
    };

    // Dados antigos sem proteção: carrega na memória desta aba e apaga do navegador
    I.cofreLegadoParaMemoria = async function () {
        for (const k of chavesAntigas()) { estado.mem.set(k, ls.getItem(k)); ls.removeItem(k); }
        try {
            for (const id of await idbChaves()) {
                const v = await idbGet(id);
                if (typeof v === 'string' && !v.startsWith('enc:')) { estado.memImg.set(id, v); await idbDel(id); }
            }
        } catch (e) { }
        return true;
    };

    // Apaga todos os dados pessoais deste navegador (preferências visuais ficam)
    I.cofreApagarTudo = async function () {
        for (const k of Object.keys(ls)) {
            if (k.startsWith(PREFIXO_CIFRADO) || ehDadoAntigo(k) || k === CHAVE_COFRE) ls.removeItem(k);
        }
        ls.setItem(CHAVE_MODO, 'sigiloso');
        try { await idbLimpar(); } catch (e) { }
        estado.mem.clear(); estado.memImg.clear(); estado.cache.clear(); estado.chave = null;
        return true;
    };
})();
