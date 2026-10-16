#!/usr/bin/env python3
"""Gera os índices em docs/api/indices/ a partir dos TOCs oficiais da Autodesk.

Uso:
    python docs/api/_tools/gerar_indices.py            # Revit 2027
    python docs/api/_tools/gerar_indices.py 2026 2027  # várias versões

Fontes:
    - Revit API Developer's Guide: https://help.autodesk.com/view/RVT/<ano>/ENU/data/toctree.json
    - Automation API (Design Automation): https://developer.doc.config.autodesk.com/bPlouYTd/design-automation_v3.json
      (método descrito em https://aps.autodesk.com/llms.txt)
"""

import datetime
import json
import pathlib
import sys
import urllib.request

RAIZ = pathlib.Path(__file__).resolve().parent.parent / "indices"
GUIDE_ID = "Revit_API_Revit_API_Developers_Guide_html"
DA_TOC = "https://developer.doc.config.autodesk.com/bPlouYTd/design-automation_v3.json"


def baixar_json(url):
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=60) as resp:
        return json.load(resp)


def achar_no(no, alvo):
    if no.get("id") == alvo:
        return no
    for filho in no.get("children", []):
        achado = achar_no(filho, alvo)
        if achado:
            return achado
    return None


def indice_revit(ano):
    toc = baixar_json(f"https://help.autodesk.com/view/RVT/{ano}/ENU/data/toctree.json")
    guia = None
    for livro in toc["books"]:
        guia = achar_no(livro, GUIDE_ID)
        if guia:
            break
    if not guia:
        raise SystemExit(f"Developer's Guide não encontrado no TOC de {ano}")

    linhas = []

    def andar(no, nivel):
        titulo = " ".join(no.get("ttl", "").split())
        url = f"https://help.autodesk.com/view/RVT/{ano}/ENU/?guid={no['id']}"
        linhas.append(f"{'  ' * nivel}- [{titulo}]({url})")
        for filho in no.get("children", []):
            andar(filho, nivel + 1)

    for filho in guia.get("children", []):
        andar(filho, 0)

    hoje = datetime.date.today().isoformat()
    cabecalho = [
        f"# Revit {ano} API Developer's Guide: índice completo",
        "",
        f"Gerado em {hoje} por `_tools/gerar_indices.py` a partir do TOC oficial "
        f"(`help.autodesk.com/view/RVT/{ano}/ENU/data/toctree.json`). {len(linhas)} páginas.",
        "",
        "O guia só existe em inglês: as URLs com `PTB` ou `ESP` carregam o mesmo conteúdo `ENU`.",
        "Para outra versão, troque o ano na URL ou rode o script com o ano desejado.",
        "",
    ]
    destino = RAIZ / f"revit-{ano}-developers-guide.md"
    destino.write_text("\n".join(cabecalho + linhas) + "\n", encoding="utf-8")
    print(f"{destino} ({len(linhas)} páginas)")


def url_portal_aps(source):
    partes = source.removesuffix(".html").split("/")
    api, resto = partes[1], partes[2:]
    if len(resto) > 2:
        resto = resto[:2] + [p for p in resto[2:] if p != api]
    return "https://aps.autodesk.com/en/docs/" + "/".join([api] + resto) + "/"


def indice_design_automation():
    toc = baixar_json(DA_TOC)
    linhas = []

    def andar(no, nivel):
        titulo = no.get("display_name")
        if titulo:
            source = no.get("source")
            item = f"[{titulo}]({url_portal_aps(source)})" if source else f"**{titulo}**"
            linhas.append(f"{'  ' * nivel}- {item}")
        for filho in no.get("children") or []:
            andar(filho, nivel + 1 if titulo else nivel)

    andar(toc, 0)

    hoje = datetime.date.today().isoformat()
    cabecalho = [
        "# Automation API (Design Automation v3): índice completo",
        "",
        f"Gerado em {hoje} por `_tools/gerar_indices.py` a partir do TOC oficial da APS "
        f"(`{DA_TOC}`). {len(linhas)} entradas.",
        "",
        "As páginas do portal `aps.autodesk.com` são renderizadas por JavaScript; "
        "para ler o HTML estático, siga o método de https://aps.autodesk.com/llms.txt.",
        "",
    ]
    destino = RAIZ / "design-automation-v3.md"
    destino.write_text("\n".join(cabecalho + linhas) + "\n", encoding="utf-8")
    print(f"{destino} ({len(linhas)} entradas)")


if __name__ == "__main__":
    RAIZ.mkdir(parents=True, exist_ok=True)
    for ano in sys.argv[1:] or ["2027"]:
        indice_revit(ano)
    indice_design_automation()
