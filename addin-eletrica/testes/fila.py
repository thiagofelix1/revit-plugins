"""Monta a fila do teste automático do AutoEletrica com um grupo da regressão (ou com ela inteira).

Uso: py -3 -I fila.py <grupo>   (apartamento | snowdon | tudo)

A regressão fica num arquivo só (regressao.json); este script escreve em Documentos\\AutoEletrica\\testes\\fila.json só os
cenários do grupo pedido. Depois é abrir o Revit: o plugin roda a fila sozinho, grava relatorio.json e concluido.txt na
mesma pasta e fecha o Revit.
"""
import io
import json
import os
import sys

GRUPOS = {
    "apartamento": ("apto-",),
    "snowdon": ("snowdon-",),
}


def grupo_de(nome):
    for grupo, prefixos in GRUPOS.items():
        if any(nome.startswith(p) for p in prefixos):
            return grupo
    return "apartamento"


def main():
    pedido = sys.argv[1] if len(sys.argv) > 1 else ""
    if pedido not in ("apartamento", "snowdon", "tudo"):
        print(__doc__)
        sys.exit(1)
    aqui = os.path.dirname(os.path.abspath(__file__))
    regressao = json.load(io.open(os.path.join(aqui, "regressao.json"), encoding="utf-8"))
    cenarios = [c for c in regressao["cenarios"] if pedido == "tudo" or grupo_de(c["nome"]) == pedido]
    destino = os.path.join(os.path.expanduser("~"), "Documents", "AutoEletrica", "testes", "fila.json")
    os.makedirs(os.path.dirname(destino), exist_ok=True)
    fila = {"fecharRevit": regressao.get("fecharRevit", True), "cenarios": cenarios}
    io.open(destino, "w", encoding="utf-8").write(json.dumps(fila, ensure_ascii=False, indent=1))
    print(f"{len(cenarios)} cenário(s) do grupo {pedido} em {destino}:")
    for c in cenarios:
        print("  " + c["nome"])
    print("Agora abra o Revit 2027. No fim, veja relatorio.json e concluido.txt na mesma pasta.")


if __name__ == "__main__":
    main()
