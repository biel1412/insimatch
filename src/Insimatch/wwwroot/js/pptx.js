// =====================================================================================
// Exportar slides do insiTalent para PowerPoint (.pptx), no próprio navegador.
// Usa PptxGenJS (hospedado junto com o site, carregado só quando necessário).
// Textos, tópicos e campos ficam EDITÁVEIS no PowerPoint.
// =====================================================================================
(function () {
    const I = window.insimatch;
    const W = 13.333, H = 7.5, MX = 0.6;           // slide 16:9 (polegadas) e margem lateral

    I._pptxLib = function () {
        if (window.PptxGenJS) return Promise.resolve(window.PptxGenJS);
        return new Promise((ok, erro) => {
            const s = document.createElement('script');
            s.src = new URL('lib/pptxgenjs/pptxgen.bundle.js', document.baseURI).href;
            s.onload = () => ok(window.PptxGenJS);
            s.onerror = () => erro(new Error('N\u00E3o foi poss\u00EDvel carregar o gerador de PowerPoint.'));
            document.head.appendChild(s);
        });
    };

    // ---------- cores ----------
    const limpa = c => (c || '').trim().replace('#', '').toUpperCase();
    const rgb = h => { h = limpa(h); if (h.length === 3) h = h.split('').map(x => x + x).join(''); const n = parseInt(h || '000000', 16); return [(n >> 16) & 255, (n >> 8) & 255, n & 255]; };
    const hex = a => a.map(v => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0')).join('').toUpperCase();
    const mix = (a, b, t) => { const x = rgb(a), y = rgb(b); return hex(x.map((v, i) => v + (y[i] - v) * t)); };
    const lum = c => { const [r, g, b] = rgb(c); return (0.299 * r + 0.587 * g + 0.114 * b) / 255; };
    const coresDoGradiente = css => (css.match(/#[0-9a-fA-F]{3,6}/g) || []);

    function varSite(nome, padrao) {
        const app = document.querySelector('.app') || document.documentElement;
        const v = getComputedStyle(app).getPropertyValue(nome).trim();
        return /^#[0-9a-fA-F]{3,6}$/.test(v) ? v : padrao;
    }

    // Fundo em imagem (degradês e o brilho padrão do site)
    function fundoImagem(base, brilho1, brilho2, gradiente) {
        const c = document.createElement('canvas'); c.width = 1600; c.height = 900;
        const g = c.getContext('2d');
        if (gradiente && gradiente.length >= 2) {
            const lg = g.createLinearGradient(0, 0, 1600, 900);       // ~135\u00B0
            gradiente.forEach((cor, i) => lg.addColorStop(i / (gradiente.length - 1), cor));
            g.fillStyle = lg; g.fillRect(0, 0, 1600, 900);
        } else {
            g.fillStyle = '#' + base; g.fillRect(0, 0, 1600, 900);
            const brilho = (x, y, r, cor, a) => {
                const rg = g.createRadialGradient(x, y, 0, x, y, r);
                const [cr, cg, cb] = rgb(cor);
                rg.addColorStop(0, `rgba(${cr},${cg},${cb},${a})`); rg.addColorStop(1, `rgba(${cr},${cg},${cb},0)`);
                g.fillStyle = rg; g.fillRect(0, 0, 1600, 900);
            };
            if (brilho1) brilho(1600, 0, 1100, brilho1, 0.16);
            if (brilho2) brilho(0, 900, 950, brilho2, 0.09);
        }
        return c.toDataURL('image/jpeg', 0.9).replace(/^data:/, '');
    }

    const tamanhoImagem = src => new Promise(ok => {
        const im = new Image(); im.onload = () => ok([im.naturalWidth, im.naturalHeight]); im.onerror = () => ok([16, 9]); im.src = src;
    });
    const semData = d => d.replace(/^data:/, '');

    // Encaixa a imagem inteira dentro da caixa, centralizada
    function caber(w, h, bx, by, bw, bh) {
        const r = Math.min(bw / w, bh / h);
        const ww = w * r, hh = h * r;
        return { x: bx + (bw - ww) / 2, y: by + (bh - hh) / 2, w: ww, h: hh };
    }

    // ---------- marca ----------
    function runsMarca(m, cores, tamanho, estilo) {
        const base = { fontSize: tamanho, bold: true, color: cores.texto, fontFace: 'Segoe UI' };
        const talent = estilo === 'texto' ? { color: cores.texto } : { color: cores.destaque };
        const nome = [{ text: 'insi', options: base }, { text: 'Talent', options: Object.assign({}, base, talent) }];
        switch (m.texto) {
            case 'insi': return [{ text: 'insi', options: Object.assign({}, base, estilo === 'texto' ? {} : { color: cores.destaque }) }];
            case 'ambos': return [{ text: ' insi ', options: Object.assign({}, base, { color: 'FFFFFF', highlight: cores.destaque }) },
                                  { text: '   |   ', options: Object.assign({}, base, { bold: false, color: cores.suave }) }].concat(nome);
            case 'personalizado': return [{ text: m.personalizado || 'Sua empresa', options: base }];
            default: return nome;
        }
    }

    I.exportarPptx = async function (d, arquivo) {
        const Pptx = await I._pptxLib();
        const pptx = new Pptx();
        pptx.layout = 'LAYOUT_WIDE';
        pptx.title = d.nome;
        pptx.company = 'insiTalent';

        const site = {
            fundo: varSite('--fundo', '#0b1020'), texto: varSite('--texto', '#eef1fb'),
            acento: varSite('--acento', '#7c6cff'), acento2: varSite('--acento-2', '#2ee6c5')
        };
        const fonteTitulo = d.fonte === 'elegante' ? 'Georgia' : 'Segoe UI';
        const fonteTexto = d.fonte === 'elegante' ? 'Georgia' : 'Segoe UI';
        const tamMarca = { p: 11, m: 14, g: 20 }[d.marca.tamanho] || 14;

        for (const s of d.slides) {
            const sl = pptx.addSlide();
            // ----- cores efetivas -----
            const gradiente = (s.fundo || '').startsWith('linear-gradient') ? coresDoGradiente(s.fundo) : null;
            const fundoBase = gradiente ? limpa(gradiente[0]) : s.fundo ? limpa(s.fundo) : mix(site.fundo, site.texto, 0.03);
            const texto = s.texto ? limpa(s.texto) : limpa(site.texto);
            const destaque = s.destaque ? limpa(s.destaque) : limpa(site.acento);
            const destaque2 = s.destaque2 ? limpa(s.destaque2) : limpa(site.acento2);
            const fundoMedio = gradiente ? mix(gradiente[0], gradiente[gradiente.length - 1], 0.5) : fundoBase;
            const escuro = lum(fundoMedio) < 0.5;
            const suave = mix(texto, fundoMedio, 0.28), apagado = mix(texto, fundoMedio, 0.45);
            const cores = { texto, destaque, suave };

            // ----- fundo -----
            const imgs = s.imagens || [];
            const capaComImagem = s.estrutura === 'capa' && imgs.length > 0;
            let corTexto = texto, corSuave = suave, corApagado = apagado;
            if (capaComImagem) {
                sl.background = { data: semData(imgs[0].dados) };
                sl.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: W, h: H, fill: { color: '050814', transparency: 38 }, line: { type: 'none' } });
                corTexto = 'FFFFFF'; corSuave = 'E6E8F0'; corApagado = 'C8CCDA';
            } else if (gradiente) {
                sl.background = { data: fundoImagem(null, null, null, gradiente) };
            } else if (!s.fundo) {
                sl.background = { data: fundoImagem(fundoBase, limpa(site.acento), limpa(site.acento2)) };
            } else {
                sl.background = { color: fundoBase };
            }
            // faixa de destaque no topo
            sl.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: W / 2, h: 0.06, fill: { color: destaque }, line: { type: 'none' } });
            sl.addShape(pptx.ShapeType.rect, { x: W / 2, y: 0, w: W / 2, h: 0.06, fill: { color: destaque2 }, line: { type: 'none' } });

            const base = { p: 13, g: 21 }[s.tamanho] || 16;
            const alinhar = s.centro ? 'center' : 'left';
            const corMarca = { texto: corTexto, destaque, suave: corSuave };
            const pos = s.marca ? d.marca.posicao : '';

            // ----- marca (topo e marca d'água) -----
            if (pos === 'fundo') {
                sl.addText(runsMarca(d.marca, corMarca, 110, 'texto').map(r => ({ text: r.text, options: Object.assign({}, r.options, { fontSize: 110, highlight: undefined, color: corTexto, transparency: 90 }) })),
                    { x: 0, y: 0, w: W, h: H, align: 'center', valign: 'middle' });
            }
            const marcaCaixa = (x, y, w, align) => {
                const runs = runsMarca(d.marca, corMarca, tamMarca, d.marca.estilo);
                const opt = { x, y, w, h: tamMarca / 72 * 1.9, align, valign: 'middle', margin: d.marca.estilo === 'selo' ? 8 : 0 };
                if (d.marca.estilo === 'selo') Object.assign(opt, { fill: { color: corTexto, transparency: 88 }, line: { color: corTexto, transparency: 75, width: 0.75 }, shape: pptx.ShapeType.roundRect, rectRadius: 0.5 });
                sl.addText(runs, opt);
            };
            let topo = 0.5;
            if (pos === 'topo-dir') marcaCaixa(W - MX - 3.2, 0.22, 3.2, 'right');
            if (pos === 'topo-esq') { marcaCaixa(MX, 0.22, 3.2, 'left'); topo = 0.9; }

            // ----- rodapé (e marca no rodapé) -----
            const yRod = H - 0.5;
            if (d.rodape || ['inf-esq', 'rodape', 'inf-dir'].includes(pos)) {
                let xNome = MX;
                if (pos === 'inf-esq') { marcaCaixa(MX, yRod - 0.06, 2.6, 'left'); xNome = MX + 2.7; }
                if (pos === 'rodape') marcaCaixa(W / 2 - 1.6, yRod - 0.06, 3.2, 'center');
                if (pos === 'inf-dir') marcaCaixa(W - MX - 2.6, yRod - 0.06, 2.6, 'right');
                if (d.rodape && !(s.estrutura === 'capa' && false)) {
                    sl.addText(d.nome || '', { x: xNome, y: yRod, w: 5, h: 0.3, fontSize: 10, color: corApagado, fontFace: fonteTexto, valign: 'middle' });
                    sl.addText(`${s.numero} / ${d.total}`, { x: W - MX - (pos === 'inf-dir' ? 3.9 : 1.5), y: yRod, w: 1.3, h: 0.3, fontSize: 10, color: corApagado, align: 'right', fontFace: fonteTexto, valign: 'middle' });
                }
            }
            const yFim = (d.rodape || ['inf-esq', 'rodape', 'inf-dir'].includes(pos)) ? yRod - 0.2 : H - 0.45;

            // ----- textos -----
            const titulo = d.fonte === 'impacto' ? (s.titulo || '').toUpperCase() : (s.titulo || '');
            const negritoTitulo = d.fonte !== 'suave';
            const linhas = s.linhas || [];
            const paragrafos = (lista, tam, cor, centro) => lista.map((l, i) => ({
                text: l.texto,
                options: Object.assign({ fontSize: tam, color: cor, fontFace: fonteTexto, breakLine: i < lista.length - 1, paraSpaceAfter: 6, align: centro ? 'center' : 'left' },
                    l.topico ? { bullet: { code: '25CF', indent: 18 } } : {})
            }));
            const ajustaTam = n => Math.max(11, Math.round(base * Math.min(1, 9 / Math.max(1, n))));

            // ---------- estruturas especiais ----------
            if (s.estrutura === 'capa') {
                const altT = titulo.length > 40 ? 1.9 : 1.2;
                const y0 = Math.max(1.4, (H - altT - 1.6) / 2);
                if (pos === 'acima') marcaCaixa(MX, y0 - 0.6, W - 2 * MX, 'center');
                sl.addText(titulo, { x: MX + 0.6, y: y0, w: W - 2 * MX - 1.2, h: altT, fontSize: 46, bold: negritoTitulo, color: corTexto, align: 'center', valign: 'bottom', fontFace: fonteTitulo, fit: 'shrink' });
                let y = y0 + altT + 0.12;
                if (s.subtitulo) { sl.addText(s.subtitulo, { x: MX + 0.6, y, w: W - 2 * MX - 1.2, h: 0.6, fontSize: 22, color: corSuave, align: 'center', fontFace: fonteTexto, fit: 'shrink' }); y += 0.7; }
                sl.addShape(pptx.ShapeType.roundRect, { x: W / 2 - 0.65, y: y + 0.05, w: 1.3, h: 0.07, rectRadius: 0.03, fill: { color: destaque }, line: { type: 'none' } });
                if (linhas.length) sl.addText(paragrafos(linhas, Math.min(base, 16), corSuave, true), { x: MX + 1, y: y + 0.3, w: W - 2 * MX - 2, h: Math.max(0.6, yFim - y - 0.4), valign: 'top', fit: 'shrink' });
                continue;
            }
            if (s.estrutura === 'secao') {
                const y0 = 2.3;
                if (pos === 'acima') marcaCaixa(MX + 0.45, y0 - 0.6, 6, 'left');
                if (s.centro) {
                    sl.addText(titulo, { x: MX, y: y0, w: W - 2 * MX, h: 1.3, fontSize: 42, bold: negritoTitulo, color: corTexto, align: 'center', valign: 'bottom', fontFace: fonteTitulo, fit: 'shrink' });
                    sl.addShape(pptx.ShapeType.roundRect, { x: W / 2 - 0.65, y: y0 + 1.45, w: 1.3, h: 0.07, rectRadius: 0.03, fill: { color: destaque }, line: { type: 'none' } });
                } else {
                    sl.addShape(pptx.ShapeType.rect, { x: MX + 0.15, y: y0, w: 0.1, h: s.subtitulo ? 2.0 : 1.3, fill: { color: destaque }, line: { type: 'none' } });
                    sl.addText(titulo, { x: MX + 0.45, y: y0, w: W - 2 * MX - 0.6, h: 1.3, fontSize: 42, bold: negritoTitulo, color: corTexto, valign: 'bottom', fontFace: fonteTitulo, fit: 'shrink' });
                }
                if (s.subtitulo) sl.addText(s.subtitulo, { x: s.centro ? MX : MX + 0.45, y: y0 + 1.35, w: W - 2 * MX - 0.6, h: 0.6, fontSize: 20, color: corSuave, align: alinhar, fontFace: fonteTexto });
                if (linhas.length) sl.addText(paragrafos(linhas, Math.min(base, 16), corSuave, s.centro), { x: s.centro ? MX + 1 : MX + 0.45, y: y0 + 2.15, w: W - 2 * MX - (s.centro ? 2 : 0.6), h: Math.max(0.6, yFim - y0 - 2.2), valign: 'top', fit: 'shrink' });
                continue;
            }

            // ---------- cabeçalho ----------
            let y = topo;
            if (pos === 'acima') { marcaCaixa(MX, y, W - 2 * MX, alinhar); y += tamMarca / 72 * 2.1; }
            if (titulo) {
                sl.addText(titulo, { x: MX + (pos === 'topo-dir' && s.centro ? 3.3 : 0), y, w: W - 2 * MX - (pos === 'topo-dir' ? (s.centro ? 6.6 : 3.3) : 0), h: 0.8, fontSize: s.tamanho === 'g' ? 36 : 30, bold: negritoTitulo, color: corTexto, align: alinhar, valign: 'middle', fontFace: fonteTitulo, fit: 'shrink' });
                y += 0.82;
            }
            if (s.subtitulo) { sl.addText(s.subtitulo, { x: MX, y, w: W - 2 * MX, h: 0.45, fontSize: 16, color: corSuave, align: alinhar, fontFace: fonteTexto, fit: 'shrink' }); y += 0.5; }
            y += 0.25;

            // ---------- corpo ----------
            const campos = s.campos || [];
            const caixa = { x: MX, y, w: W - 2 * MX, h: yFim - y };

            if (s.estrutura === 'destaque') {
                const texto = linhas.map(l => l.texto).join('\n');
                sl.addText('\u201C', { x: s.centro ? W / 2 - 0.5 : MX, y: caixa.y, w: 1, h: 1.1, fontSize: 80, color: destaque, fontFace: 'Georgia', align: s.centro ? 'center' : 'left', valign: 'top' });
                sl.addText(texto, { x: s.centro ? MX + 0.6 : MX + 0.9, y: caixa.y + (s.centro ? 0.9 : 0.25), w: caixa.w - (s.centro ? 1.2 : 0.9), h: caixa.h - 1, fontSize: Math.max(22, base + 12), bold: true, color: corTexto, align: alinhar, valign: 'middle', fontFace: fonteTexto, fit: 'shrink' });
                continue;
            }

            // imagens: ao lado (padrão) ou abaixo (conteúdo curto + imagens horizontais)
            const temConteudo = linhas.length > 0 || campos.length > 0;
            const dims = [];
            for (const im of imgs) dims.push(im.w > 0 && im.h > 0 ? [im.w, im.h] : await tamanhoImagem(im.dados));
            const paisagem = dims.every(([w, h]) => w >= h * 1.15);
            const curto = campos.length <= 2 && linhas.length <= 3 && campos.length + linhas.length <= 3;
            let areaConteudo = caixa, areaImagens = null, imagensAbaixo = false;
            if (imgs.length && !temConteudo) areaImagens = caixa;
            else if (imgs.length && curto && paisagem) {
                imagensAbaixo = true;
                const hc = Math.min(caixa.h * 0.42, 0.45 * linhas.length + (campos.length ? 1.45 : 0) + 0.2);
                areaConteudo = { x: caixa.x, y: caixa.y, w: caixa.w, h: hc };
                areaImagens = { x: caixa.x, y: caixa.y + hc + 0.15, w: caixa.w, h: caixa.h - hc - 0.15 };
            } else if (imgs.length) {
                const wi = caixa.w * (imgs.length > 1 ? 0.44 : 0.46), gap = 0.35;
                const ladoEsq = s.inverter;
                areaImagens = { x: ladoEsq ? caixa.x : caixa.x + caixa.w - wi, y: caixa.y, w: wi, h: caixa.h };
                areaConteudo = { x: ladoEsq ? caixa.x + wi + gap : caixa.x, y: caixa.y, w: caixa.w - wi - gap, h: caixa.h };
            }
            if (imagensAbaixo && s.inverter && areaImagens) {      // imagens acima
                const t = areaConteudo.h;
                areaImagens = { x: caixa.x, y: caixa.y, w: caixa.w, h: caixa.h - t - 0.15 };
                areaConteudo = { x: caixa.x, y: caixa.y + areaImagens.h + 0.15, w: caixa.w, h: t };
            }

            // texto
            let yC = areaConteudo.y;
            if (linhas.length) {
                const tam = ajustaTam(linhas.length);
                const hTexto = campos.length ? Math.min(areaConteudo.h * 0.55, linhas.length * tam / 72 * 1.75 + 0.15) : areaConteudo.h;
                if (s.estrutura === 'colunas' && linhas.length > 1) {
                    const meio = Math.ceil(linhas.length / 2), wc = (areaConteudo.w - 0.4) / 2;
                    sl.addText(paragrafos(linhas.slice(0, meio), tam, corTexto, false), { x: areaConteudo.x, y: yC, w: wc, h: hTexto, valign: 'top', fit: 'shrink' });
                    sl.addText(paragrafos(linhas.slice(meio), tam, corTexto, false), { x: areaConteudo.x + wc + 0.4, y: yC, w: wc, h: hTexto, valign: 'top', fit: 'shrink' });
                } else {
                    sl.addText(paragrafos(linhas, tam, corTexto, s.centro), { x: areaConteudo.x, y: yC, w: areaConteudo.w, h: hTexto, valign: 'top', fit: 'shrink' });
                }
                yC += hTexto + 0.2;
            }

            // campos em cartões (até 4 por linha; 2 quando divide espaço com imagens)
            if (campos.length) {
                const porLinha = Math.min(campos.length, areaConteudo.w < 8 ? 2 : 4);
                const linhasC = Math.ceil(campos.length / porLinha), gap = 0.22;
                const wc = (areaConteudo.w - gap * (porLinha - 1)) / porLinha;
                const hc = Math.max(0.95, Math.min(1.5, (areaConteudo.y + areaConteudo.h - yC - gap * (linhasC - 1)) / linhasC));
                const corStatus = n => ({
                    Excelente: escuro ? '2FE0A0' : '059669', Bom: escuro ? '2FE0A0' : '059669',
                    Atencao: escuro ? 'FFC857' : 'B45309', Critico: escuro ? 'FF6B81' : 'DC2626'
                }[n] || corApagado);
                campos.forEach((c, i) => {
                    const cx = areaConteudo.x + (i % porLinha) * (wc + gap);
                    const cy = yC + Math.floor(i / porLinha) * (hc + gap);
                    const tc = c.corTexto ? limpa(c.corTexto) : (c.corFundo ? (lum(c.corFundo) > 0.55 ? '0F172A' : 'FFFFFF') : corTexto);
                    sl.addShape(pptx.ShapeType.roundRect, {
                        x: cx, y: cy, w: wc, h: hc, rectRadius: 0.12,
                        fill: c.corFundo ? { color: limpa(c.corFundo) } : { color: corTexto, transparency: 93 },
                        line: { color: c.nivel ? corStatus(c.nivel) : corTexto, transparency: c.nivel ? 55 : 82, width: 1 }
                    });
                    const tcSuave = mix(tc, c.corFundo ? limpa(c.corFundo) : fundoMedio, 0.3);
                    const partes = [
                        { text: c.nome || '', options: { fontSize: 11, color: tcSuave, breakLine: true } },
                        { text: c.valor || '\u2014', options: { fontSize: hc > 1.2 ? 24 : 20, bold: true, color: tc, breakLine: !!(c.meta || c.status) } }
                    ];
                    if (c.meta || c.status) partes.push({ text: [c.status, c.meta].filter(Boolean).join(' \u00B7 '), options: { fontSize: 10, color: c.nivel ? corStatus(c.nivel) : tcSuave } });
                    sl.addText(partes, { x: cx + 0.12, y: cy + 0.06, w: wc - 0.24, h: hc - 0.12, valign: 'middle', fontFace: fonteTexto, fit: 'shrink' });
                });
            }

            // imagens (inteiras, centralizadas na área)
            if (areaImagens && imgs.length) {
                const n = imgs.length, gap = 0.25;
                const lado = !imagensAbaixo && temConteudo;          // empilha quando est\u00E3o ao lado do texto
                imgs.forEach((im, i) => {
                    const legenda = im.legenda ? 0.32 : 0;
                    let bx, by, bw, bh;
                    if (n === 1) { bx = areaImagens.x; by = areaImagens.y; bw = areaImagens.w; bh = areaImagens.h - legenda; }
                    else if (lado) { bh = (areaImagens.h - gap) / 2 - legenda; bw = areaImagens.w; bx = areaImagens.x; by = areaImagens.y + i * (bh + legenda + gap); }
                    else { bw = (areaImagens.w - gap) / 2; bh = areaImagens.h - legenda; bx = areaImagens.x + i * (bw + gap); by = areaImagens.y; }
                    const r = caber(dims[i][0], dims[i][1], bx, by, bw, bh);
                    sl.addImage({ data: semData(im.dados), x: r.x, y: r.y, w: r.w, h: r.h, rounding: false });
                    if (legenda) sl.addText(im.legenda, { x: bx, y: r.y + r.h + 0.04, w: bw, h: 0.28, fontSize: 10, color: corApagado, align: 'center', fontFace: fonteTexto });
                });
            }
        }
        await pptx.writeFile({ fileName: arquivo });
        return true;
    };
})();
