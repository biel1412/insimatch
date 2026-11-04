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
    telaCheia: function () {
        try {
            if (!document.fullscreenElement) document.documentElement.requestFullscreen();
            else document.exitFullscreen();
        } catch (e) { /* não suportado */ }
    }
};
