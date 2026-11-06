namespace Eletrica.Motor.Calculo;

/// <summary>O que fazer quando um critério reprova (item 8.8 do plano): a sugestão vai junto com o problema, pelo critério.</summary>
public static class Sugestoes
{
    public static string Para(string criterio) => criterio switch
    {
        "capacidade de corrente" => "subir a seção, reduzir o agrupamento no trecho crítico (separar circuitos em outro eletroduto) ou dividir o circuito",
        "sobrecarga" => "não há In entre IB e IZ: subir a seção ou dividir o circuito",
        "queda de tensão" => "subir a seção, encurtar o caminho (outro traçado) ou dividir o circuito; se a queda a montante pesa, subir o alimentador",
        "capacidade de interrupção" => "disjuntor com Icn maior (6 ou 10 kA) ou limitar a Icc no quadro",
        "solicitação térmica no curto" => "informar o I²t de catálogo do disjuntor na Icc presumida, usar disjuntor limitador ou subir a seção",
        "seccionamento automático" => "subir a seção (baixa Zs), trocar a curva do disjuntor para B (menor Ia) ou encurtar o circuito; em TT, DR",
        "seção mínima" => "usar a seção mínima da Tabela 47 para o tipo de circuito",
        "taxa de ocupação" => "subir o DN do eletroduto, repetir o eletroduto ou tirar circuitos do trecho (Fiação manual)",
        "mesma origem" => "tirar do eletroduto os circuitos de outro quadro (Fiação manual: evitar) ou traçar outro eletroduto",
        "seções próximas" => "separar em eletrodutos os circuitos de seções muito diferentes ou aceitar o agrupamento 1/√n",
        "trecho entre caixas" => "pôr caixa de passagem (Caixas por regra) ou subir o DN um tamanho a cada 6 m de excesso",
        "curvas" => "pôr caixa de passagem entre as curvas (Caixas por regra)",
        "tamanho mínimo" => "trocar por eletroduto DN 16 ou maior",
        "eletroduto" => "escolher um DN da NBR 15465 para o tipo de eletroduto",
        _ => "rever o circuito",
    };
}
