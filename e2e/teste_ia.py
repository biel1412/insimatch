import json, sys, time, os, atexit
from playwright.sync_api import sync_playwright
URL = "https://biel1412.github.io/insimatch/teste/"
PLAN = sys.argv[1]; OUT = sys.argv[2]
os.makedirs(OUT, exist_ok=True)
log = {"testes": [], "groq": [], "console": []}
atexit.register(lambda: json.dump(log, open(f"{OUT}/resultado.json", "w"), ensure_ascii=False, indent=2))

def rodar(page, nome, slides):
    t0 = time.time()
    page.click(".btn-ia")
    page.wait_for_selector(".dialogo-ia")
    sem_chave = page.locator(".ia-chave").count() == 0
    page.set_input_files(".dialogo-ia input[type=file]", PLAN)
    page.wait_for_selector(".ia-planilha", timeout=20000)
    if slides:
        page.click(".dialogo-ia >> text=Apresentação em slides")
    page.fill(".dialogo-ia input[maxlength='40']", nome)
    page.click(".dialogo-ia >> text=✨ Gerar")
    erro = None
    try:
        page.wait_for_function("() => !document.querySelector('.dialogo-ia') || document.querySelector('.ia-erro')", timeout=95000)
    except Exception as e:
        erro = "timeout"
    if page.locator(".ia-erro").count():
        erro = page.locator(".ia-erro").inner_text()
    page.wait_for_timeout(2500)
    if page.locator(".dialogo-ia").count():
        page.screenshot(path=f"{OUT}/erro_{'slides' if slides else 'painel'}.png")
        page.click(".dialogo-x")
        page.wait_for_timeout(500)
    page.screenshot(path=f"{OUT}/{'slides' if slides else 'painel'}.png", full_page=True)
    texto = page.inner_text("main") if page.locator("main").count() else page.inner_text("body")
    log["testes"].append({"nome": nome, "slides": slides, "secao_chave_oculta": sem_chave,
                          "erro": erro, "segundos": round(time.time()-t0, 1), "texto": texto[:3000]})

with sync_playwright() as p:
    b = p.chromium.launch()
    page = b.new_page(viewport={"width": 1440, "height": 900})
    page.on("console", lambda m: log["console"].append(f"{m.type}: {m.text}") if m.type in ("error","warning") else None)
    def resp(r):
        if "api.groq.com" in r.url:
            try: corpo = r.text()
            except Exception: corpo = ""
            info = {"status": r.status}
            try:
                j = json.loads(corpo); info["modelo"] = j.get("model"); info["uso"] = j.get("usage")
                info["conteudo"] = j["choices"][0]["message"]["content"][:4000]
            except Exception: info["corpo"] = corpo[:1500]
            log["groq"].append(info)
    page.on("response", resp)
    ok = False
    for tentativa in range(20):
        page.goto(URL + "?v=" + str(time.time()), wait_until="networkidle")
        page.wait_for_selector(".btn-ia", timeout=60000)
        ok = page.evaluate("() => !!(window.insimatch && window.insimatch.chaveIA && window.insimatch.chaveIA())")
        if ok: break
        time.sleep(20)
    log["chave_carregada"] = ok
    for nome, sl in (("IA Painel Teste", False), ("IA Slides Teste", True)):
        try: rodar(page, nome, sl)
        except Exception as e: log["testes"].append({"nome": nome, "excecao": str(e)[:500]})
    # Navega pelos slides gerados
    for i in range(1, 4):
        page.keyboard.press("ArrowRight"); page.wait_for_timeout(700)
        page.screenshot(path=f"{OUT}/slides_{i+1}.png")
    b.close()
json.dump(log, open(f"{OUT}/resultado.json", "w"), ensure_ascii=False, indent=2)
print(json.dumps({k: v for k, v in log.items() if k != "groq"}, ensure_ascii=False)[:3000])
