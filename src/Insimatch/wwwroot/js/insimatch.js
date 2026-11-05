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
    telaCheia: function () {
        try {
            if (!document.fullscreenElement) document.documentElement.requestFullscreen();
            else document.exitFullscreen();
        } catch (e) { /* não suportado */ }
    }
};
