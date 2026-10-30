"""Monta a fila do teste automático com um grupo da regressão (ou com ela inteira).

Uso: python -I fila.py <grupo>   (calculo | assistida | geracao | nova-era | tudo)

A regressão continua num arquivo só (regressao.json); este script escreve em Documentos\\SPK\\testes\\fila.json só os
cenários do grupo pedido, para rodar em pedaços menores. Cenário novo que não cair em nenhum grupo vai para
"assistida" e aparece no aviso do fim.
"""
import io
import json
import os
import sys

GRUPOS = {
    "calculo": ("ex-", "sab-", "nova-era-folhas", "nova-era-colisao", "projeto2-"),
    "geracao": ("cena-arquitetura", "cena-bicos-ambiente", "cena-gerar-rede", "cena-setor-", "cena-corredor-l", "cena-vga-meio",
                "cena-vigas", "cena-grelha-"),
    "nova-era": ("arquitetura-nova-era", "nova-era-reconectar", "nova-era-derivacoes", "nova-era-calculo"),
}
NOVA_ERA_EXATO = {"nova-era"}


def grupo_de(nome):
    if nome in NOVA_ERA_EXATO:
        return "nova-era"
    for grupo, prefixos in GRUPOS.items():
        if any(nome == p or nome.startswith(p) for p in prefixos):
            return grupo
    return "assistida"


def main():
    pedido = sys.argv[1] if len(sys.argv) > 1 else ""
    if pedido not in ("calculo", "assistida", "geracao", "nova-era", "tudo"):
        print(__doc__)
        sys.exit(1)
    aqui = os.path.dirname(os.path.abspath(__file__))
    regressao = json.load(io.open(os.path.join(aqui, "regressao.json"), encoding="utf-8"))
    cenarios = [c for c in regressao["cenarios"] if pedido == "tudo" or grupo_de(c["nome"]) == pedido]
    destino = os.path.join(os.path.expanduser("~"), "Documents", "SPK", "testes", "fila.json")
    os.makedirs(os.path.dirname(destino), exist_ok=True)
    fila = {"fecharRevit": regressao.get("fecharRevit", True), "cenarios": cenarios}
    io.open(destino, "w", encoding="utf-8").write(json.dumps(fila, ensure_ascii=False, indent=1))
    print(f"{len(cenarios)} cenário(s) do grupo {pedido} em {destino}:")
    for c in cenarios:
        print("  " + c["nome"])
    if pedido == "assistida":
        soltos = [c["nome"] for c in cenarios if not c["nome"].startswith("cena-")]
        if soltos:
            print("Aviso: estes não têm grupo próprio e caíram aqui: " + ", ".join(soltos))


if __name__ == "__main__":
    main()
