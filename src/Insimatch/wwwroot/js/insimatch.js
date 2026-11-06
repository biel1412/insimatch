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
    }
};
