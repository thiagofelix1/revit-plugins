"""Monta a fila do teste automático do AutoEletrica com um grupo da regressão (ou com ela inteira).

Uso: py -3 -I fila.py <grupo>   (apartamento | snowdon | pesado | tudo | completo)

  apartamento  os dois cenários da cena do apartamento (uns 2 min): rodar depois de toda mudança
  snowdon      o Snowdon até a Lista de material, sem Quadros e Pranchas (uns 5 min)
  pesado       Quadros e Pranchas no Snowdon (169 quadros; 30 min ou mais e muita memória): só quando mexer nesses
               passos, sozinho, com a máquina livre
  tudo         apartamento + snowdon (o dia a dia)
  completo     tudo + pesado

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
    "pesado": ("snowdon-entregaveis",),
    "snowdon": ("snowdon-",),
}
DIA_A_DIA = ("apartamento", "snowdon")


def grupo_de(nome):
    for grupo, prefixos in GRUPOS.items():
        if any(nome.startswith(p) for p in prefixos):
            return grupo
    return "apartamento"


def main():
    pedido = sys.argv[1] if len(sys.argv) > 1 else ""
    if pedido not in ("apartamento", "snowdon", "pesado", "tudo", "completo"):
        print(__doc__)
        sys.exit(1)
    aqui = os.path.dirname(os.path.abspath(__file__))
    regressao = json.load(io.open(os.path.join(aqui, "regressao.json"), encoding="utf-8"))
    def entra(nome):
        grupo = grupo_de(nome)
        if pedido == "completo":
            return True
        if pedido == "tudo":
            return grupo in DIA_A_DIA
        return grupo == pedido

    cenarios = [c for c in regressao["cenarios"] if entra(c["nome"])]
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
