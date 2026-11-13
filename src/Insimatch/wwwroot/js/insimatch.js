window.insimatch = {
    ler: function (chave) {
        try { return window.localStorage.getItem(chave); } catch (e) { return null; }
    },
    gravar: function (chave, valor) {
        try { window.localStorage.setItem(chave, valor); } catch (e) { /* armazenamento indisponível */ }
    },
    remover: function (chave) {
        try { window.localStorage.removeItem(chave); } catch (e) { /* armazenamento indisponível */ }
    },
    baixar: function (nomeArquivo, conteudo, tipo) {
        const blob = new Blob(["﻿" + conteudo], { type: tipo || "text/csv;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = nomeArquivo;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    },
    baixarBytes: function (nomeArquivo, bytes, tipo) {
        const blob = new Blob([bytes], { type: tipo || "application/octet-stream" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = nomeArquivo;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1500);
    },
    imprimir: function () {
        window.print();
    },
    baixarRelatorio: async function (id, nomeArquivo, titulo) {
        const folha = document.getElementById(id);
        if (!folha) return;
        const tema = (document.querySelector('.app') || {}).dataset?.tema || 'noturno';
        let css = '';
        try { css = await (await fetch('css/app.css')).text(); } catch (e) { /* sem estilos */ }
        const html = '<!DOCTYPE html><html lang="pt-BR"><head><meta charset="utf-8">' +
            '<meta name="viewport" content="width=device-width, initial-scale=1">' +
            '<title>' + titulo.replace(/</g, '&lt;') + '</title>' +
            '<link rel="preconnect" href="https://fonts.googleapis.com">' +
            '<link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&display=swap" rel="stylesheet">' +
            '<style>' + css + '</style></head>' +
            '<body class="rel-avulso"><div data-tema="' + tema + '">' + folha.outerHTML + '</div></body></html>';
        window.insimatch.baixar(nomeArquivo, html, 'text/html;charset=utf-8');
    },
    // ---------- Imagens dos slides (IndexedDB: muito mais espaço que o localStorage) ----------
    _db: null,
    _abrirDb: function () {
        if (window.insimatch._db) return Promise.resolve(window.insimatch._db);
        return new Promise(function (ok, erro) {
            const r = indexedDB.open('insitalent', 1);
            r.onupgradeneeded = function () { r.result.createObjectStore('imagens'); };
            r.onsuccess = function () { window.insimatch._db = r.result; ok(r.result); };
            r.onerror = function () { erro(r.error); };
        });
    },
    imgSalvar: async function (id, dataUrl) {
        const db = await window.insimatch._abrirDb();
        return new Promise(function (ok, erro) {
            const tx = db.transaction('imagens', 'readwrite');
            tx.objectStore('imagens').put(dataUrl, id);
            tx.oncomplete = function () { ok(true); };
            tx.onerror = function () { erro(tx.error); };
        });
    },
    imgLer: async function (ids) {
        const db = await window.insimatch._abrirDb();
        const resultado = {};
        await Promise.all(ids.map(function (id) {
            return new Promise(function (ok) {
                const r = db.transaction('imagens', 'readonly').objectStore('imagens').get(id);
                r.onsuccess = function () { if (r.result) resultado[id] = r.result; ok(); };
                r.onerror = function () { ok(); };
            });
        }));
        return resultado;
    },
    imgRemover: async function (id) {
        try {
            const db = await window.insimatch._abrirDb();
            db.transaction('imagens', 'readwrite').objectStore('imagens').delete(id);
        } catch (e) { /* ignora */ }
    },
    imgDimensoes: function (dataUrl) {
        return new Promise(function (ok) {
            const img = new Image();
            img.onload = function () { ok([img.naturalWidth, img.naturalHeight]); };
            img.onerror = function () { ok([0, 0]); };
            img.src = dataUrl;
        });
    },
    // Setas do teclado para trocar de slide (ignora quando o foco está num campo)
    _teclas: null,
    teclasSlides: function (ref) {
        if (window.insimatch._teclas) {
            document.removeEventListener('keydown', window.insimatch._teclas);
            window.insimatch._teclas = null;
        }
        if (!ref) return;
        const h = function (e) {
            const t = e.target;
            if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.tagName === 'SELECT' || t.isContentEditable)) return;
            let p = 0;
            if (e.key === 'ArrowRight' || e.key === 'PageDown') p = 1;
            else if (e.key === 'ArrowLeft' || e.key === 'PageUp') p = -1;
            if (!p) return;
            e.preventDefault();
            ref.invokeMethodAsync('Tecla', p);
        };
        window.insimatch._teclas = h;
        document.addEventListener('keydown', h);
    },
    telaCheia: function () {
        try {
            if (!document.fullscreenElement) document.documentElement.requestFullscreen();
            else document.exitFullscreen();
        } catch (e) { /* não suportado */ }
    },
    // Colar imagens (Ctrl+V) no "Gerar com IA": reduz no navegador e entrega ao .NET em base64
    _colar: null,
    colarImagens: function (ref) {
        if (window.insimatch._colar) {
            document.removeEventListener('paste', window.insimatch._colar);
            window.insimatch._colar = null;
        }
        if (!ref) return;
        const h = function (e) {
            const itens = (e.clipboardData && e.clipboardData.items) || [];
            let achou = false;
            for (const it of itens) {
                if (!it.type || it.type.indexOf('image/') !== 0) continue;
                const arq = it.getAsFile();
                if (!arq) continue;
                achou = true;
                const url = URL.createObjectURL(arq);
                const img = new Image();
                img.onload = function () {
                    const max = 2400, esc = Math.min(1, max / Math.max(img.width, img.height));
                    const c = document.createElement('canvas');
                    c.width = Math.round(img.width * esc); c.height = Math.round(img.height * esc);
                    const g = c.getContext('2d');
                    g.fillStyle = '#fff'; g.fillRect(0, 0, c.width, c.height);
                    g.drawImage(img, 0, 0, c.width, c.height);
                    URL.revokeObjectURL(url);
                    const b64 = c.toDataURL('image/jpeg', 0.92).split(',')[1];
                    ref.invokeMethodAsync('ImagemColada', 'image/jpeg', b64);
                };
                img.src = url;
            }
            if (achou) e.preventDefault();
        };
        window.insimatch._colar = h;
        document.addEventListener('paste', h);
    },
    // ---------- PDF: recorta figuras indicadas pela IA (pdf.js carregado só quando precisa) ----------
    _pdfjs: null,
    _pdfLib: async function () {
        if (!window.insimatch._pdfjs) {
            const base = document.baseURI;
            const lib = await import(new URL('lib/pdfjs/pdf.min.mjs', base).href);
            lib.GlobalWorkerOptions.workerSrc = new URL('lib/pdfjs/pdf.worker.min.mjs', base).href;
            window.insimatch._pdfjs = lib;
        }
        return window.insimatch._pdfjs;
    },
    // pedidos: [{ pagina, recorte: [ymin, xmin, ymax, xmax] (0–1000) ou null }] → [{ dataUrl, w, h } ou null]
    pdfRecortar: async function (base64, pedidos) {
        const lib = await window.insimatch._pdfLib();
        const bin = Uint8Array.from(atob(base64), function (c) { return c.charCodeAt(0); });
        const tarefa = lib.getDocument({ data: bin });
        const doc = await tarefa.promise;
        const saida = [];
        for (const p of pedidos) {
            try {
                if (!p || p.pagina < 1 || p.pagina > doc.numPages) { saida.push(null); continue; }
                const pag = await doc.getPage(p.pagina);
                const v1 = pag.getViewport({ scale: 1 });
                let r = (p.recorte && p.recorte.length === 4) ? p.recorte.map(function (n) { return Math.min(1000, Math.max(0, n)) / 1000; }) : [0, 0, 1, 1];
                let y0 = r[0], x0 = r[1], y1 = r[2], x1 = r[3];
                if (y1 - y0 < 0.02 || x1 - x0 < 0.02) { y0 = 0; x0 = 0; y1 = 1; x1 = 1; }
                const m = 0.012;
                y0 = Math.max(0, y0 - m); x0 = Math.max(0, x0 - m); y1 = Math.min(1, y1 + m); x1 = Math.min(1, x1 + m);
                const escala = Math.min(4, Math.max(1, 1600 / (v1.width * (x1 - x0))));
                const vp = pag.getViewport({ scale: escala });
                const c = document.createElement('canvas');
                c.width = Math.ceil(vp.width); c.height = Math.ceil(vp.height);
                const g = c.getContext('2d');
                g.fillStyle = '#fff'; g.fillRect(0, 0, c.width, c.height);
                await pag.render({ canvas: c, canvasContext: g, viewport: vp }).promise;
                const sx = Math.floor(x0 * c.width), sy = Math.floor(y0 * c.height);
                const sw = Math.max(1, Math.floor((x1 - x0) * c.width)), sh = Math.max(1, Math.floor((y1 - y0) * c.height));
                const o = document.createElement('canvas');
                o.width = sw; o.height = sh;
                o.getContext('2d').drawImage(c, sx, sy, sw, sh, 0, 0, sw, sh);
                saida.push({ dataUrl: o.toDataURL('image/jpeg', 0.9), w: sw, h: sh });
                pag.cleanup();
            } catch (e) { console.warn('insiTalent: recorte do PDF falhou', e); saida.push(null); }
        }
        try { await tarefa.destroy(); } catch (e) { }
        return saida;
    }
};
