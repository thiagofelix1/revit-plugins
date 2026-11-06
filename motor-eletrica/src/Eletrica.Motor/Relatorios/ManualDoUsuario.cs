using System.Text;
using Eletrica.Motor.Calculo;
using Eletrica.Motor.Normas;
using static Eletrica.Motor.Relatorios.Docx;
using static Eletrica.Motor.Relatorios.Memorial;

namespace Eletrica.Motor.Relatorios;

/// <summary>
///     Manual do usuário da instalação (NBR 5410 6.1.8.3; item 9.10 do plano): o que o morador ou o responsável pelo prédio
///     precisa saber para usar e manter a instalação. Escrito para leigo, em Markdown e .docx: o que cada disjuntor desliga,
///     o que fazer quando um disjuntor ou o DR desarma, o teste mensal do DR, o DPS, a advertência obrigatória de 6.5.4.10,
///     cuidados nas áreas molhadas e quando chamar um profissional. Ninguém no mercado gera este documento.
/// </summary>
public static class ManualDoUsuario
{
    public static string GerarMarkdown(ResultadoInstalacao r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Manual do usuário da instalação elétrica — {r.Nome}");
        sb.AppendLine();
        foreach (var secao in Secoes(r))
        {
            sb.AppendLine($"## {secao.Titulo}");
            sb.AppendLine();
            foreach (var p in secao.Paragrafos) sb.AppendLine(p + "\n");
            if (secao.Tabela is { } t)
            {
                sb.AppendLine("| " + string.Join(" | ", t.Cabecalho) + " |");
                sb.AppendLine("|" + string.Concat(t.Cabecalho.Select(_ => "---|")));
                foreach (var l in t.Linhas) sb.AppendLine("| " + string.Join(" | ", l) + " |");
                sb.AppendLine();
            }
            foreach (var i in secao.Itens) sb.AppendLine("- " + i);
            if (secao.Itens.Count > 0) sb.AppendLine();
        }
        return sb.ToString();
    }

    public static byte[] GerarDocx(ResultadoInstalacao r) => Documento(body =>
    {
        body.AppendChild(Titulo($"Manual do usuário da instalação elétrica — {r.Nome}"));
        foreach (var secao in Secoes(r))
        {
            body.AppendChild(Subtitulo(secao.Titulo));
            foreach (var p in secao.Paragrafos) body.AppendChild(Paragrafo(p));
            if (secao.Tabela is { } t) body.AppendChild(Tabela(t.Cabecalho, t.Linhas, fonteMenor: true));
            foreach (var i in secao.Itens) body.AppendChild(Item(i));
        }
    });

    public sealed record TabelaDoManual(IReadOnlyList<string> Cabecalho, IReadOnlyList<IReadOnlyList<string>> Linhas);

    public sealed record SecaoDoManual(string Titulo, IReadOnlyList<string> Paragrafos, TabelaDoManual? Tabela = null, IReadOnlyList<string>? ItensOpcionais = null)
    {
        public IReadOnlyList<string> Itens => ItensOpcionais ?? [];
    }

    public static IReadOnlyList<SecaoDoManual> Secoes(ResultadoInstalacao r)
    {
        var a = r.Alimentacao;
        var secoes = new List<SecaoDoManual>();

        secoes.Add(new SecaoDoManual("Para que serve este manual",
        [
            "Este manual acompanha o projeto elétrico e fica com quem usa a instalação. Ele explica, em linguagem simples, o que há no quadro de luz, o que cada chave desliga, " +
            "o que fazer quando uma delas desarma e quais cuidados mantêm a instalação segura. A ABNT NBR 5410 exige que toda instalação seja entregue com um documento assim (item 6.1.8.3).",
            $"A instalação é alimentada em {F(a.TensaoFaseNeutro, 0)} V entre fase e neutro e {F(a.TensaoFaseFase, 0)} V entre fases, com {a.Fases} fase(s). " +
            "Tomadas comuns são de 127 V ou 220 V conforme indicado no espelho; ligar um aparelho na tensão errada queima o aparelho.",
        ]));

        foreach (var q in r.Quadros)
        {
            var circuitos = r.Circuitos.Where(c => c.Quadro == q.Id).ToList();
            var linhas = circuitos.Select(c => (IReadOnlyList<string>)
            [
                c.Numero, c.Descricao, $"{c.Disjuntor.In:0.#} A", c.DR.Obrigatorio ? "sim" : "não", $"{F(c.PotenciaVA, 0)} VA",
            ]).ToList();
            var paragrafos = new List<string>
            {
                $"O quadro {q.Nome} tem {q.NumeroDeCircuitos} circuito(s) e {q.Reserva} espaço(s) de reserva para ampliações. " +
                (q.DisjuntorGeral is { } g ? $"O disjuntor geral ({g.In:0.#} A) desliga tudo o que sai deste quadro. " : "") +
                "Cada disjuntor menor protege um circuito; a tabela abaixo diz o que cada um alimenta.",
            };
            if (q.Dps is not null)
                paragrafos.Add("Este quadro tem dispositivo de proteção contra surtos (DPS), que protege os aparelhos contra as sobretensões de raios e manobras da rede. " +
                               "O DPS tem um indicador: quando muda de cor (em geral para vermelho), ele cumpriu a função e precisa ser trocado por um eletricista. Não retire o DPS.");
            secoes.Add(new SecaoDoManual($"Quadro {q.Nome}: o que cada disjuntor desliga", paragrafos,
                new TabelaDoManual(["Circuito", "O que alimenta", "Disjuntor", "Com DR", "Potência"], linhas)));
        }

        var comDr = r.Circuitos.Where(c => c.DR.Obrigatorio).ToList();
        secoes.Add(new SecaoDoManual("O DR: a chave que protege contra choques",
        [
            "O DR (dispositivo diferencial-residual) desliga o circuito em frações de segundo quando a corrente \"escapa\" do caminho normal, por exemplo por uma pessoa que tocou numa parte energizada " +
            "ou por um aparelho com defeito. Ele é obrigatório nos circuitos de banheiro, cozinha, área de serviço, área externa e de chuveiro (NBR 5410 5.1.3.2.2).",
            comDr.Count > 0 ? "Circuitos protegidos por DR nesta instalação: " + string.Join(", ", comDr.Select(c => $"{c.Numero} ({c.Descricao})")) + "." : "Nenhum circuito desta instalação exige DR pela norma; mesmo assim, é uma proteção recomendada.",
        ], ItensOpcionais:
        [
            "Teste o DR uma vez por mês: aperte o botão \"T\" (teste). Ele deve desarmar na hora. Religue em seguida. Se não desarmar, chame um eletricista: o DR está com defeito.",
            "Se o DR desarma sozinho: desligue todos os disjuntores dos circuitos dele, religue o DR e vá religando os disjuntores um a um. O circuito que fizer o DR cair de novo tem um aparelho ou uma fiação com fuga; tire os aparelhos da tomada desse circuito e tente de novo. Se cair sem nenhum aparelho ligado, chame um eletricista.",
            "Nunca retire, desligue de vez ou substitua o DR por um disjuntor comum porque \"ele fica desarmando\". Desarmes frequentes são sinal de defeito na instalação ou num aparelho, não do DR.",
        ]));

        secoes.Add(new SecaoDoManual("Quando um disjuntor desarma",
        [
            "O disjuntor desarma por sobrecarga (aparelhos demais no mesmo circuito) ou por curto-circuito (defeito num aparelho, numa tomada ou na fiação). " +
            "Desligue os aparelhos daquele circuito, religue o disjuntor e volte a ligar os aparelhos um a um. Se ele desarmar imediatamente sem nada ligado, há um curto: chame um eletricista.",
        ], ItensOpcionais: [Nbr5410.AdvertenciaDoQuadro]));

        secoes.Add(new SecaoDoManual("Cuidados no dia a dia",
        [
            "A instalação foi calculada para as cargas do projeto. Aparelhos de alta potência (chuveiro, ar-condicionado, forno, micro-ondas, máquina de lavar) têm circuitos próprios: " +
            "use-os só nas tomadas e pontos previstos para eles.",
        ], ItensOpcionais:
        [
            "Não use benjamins (\"tês\") nem extensões de forma permanente: a tomada e o circuito foram dimensionados para uma carga; somar aparelhos sobrecarrega o fio dentro da parede.",
            "Tomadas de 20 A (pino mais grosso) são para aparelhos pesados; não force um plugue de 20 A numa tomada de 10 A com adaptador.",
            "No banheiro, não use aparelhos elétricos dentro do boxe nem junto da banheira; o chuveiro elétrico não pode ser ligado por tomada nem ter a resistência trocada com o disjuntor ligado.",
            "Não troque o chuveiro ou o ar-condicionado por outro de potência maior sem conferir com um eletricista: o fio e o disjuntor foram escolhidos para a potência do projeto.",
            "Se cheirar a queimado, uma tomada esquentar ou aparecer fumaça, desligue o disjuntor geral e chame um eletricista.",
            "Antes de furar paredes, consulte a planta elétrica: os eletrodutos passam dentro delas.",
            "Toda alteração na instalação (novo ponto, novo circuito) deve ser feita por profissional habilitado e registrada no projeto.",
        ]));

        secoes.Add(new SecaoDoManual("Manutenção e revisões",
        [
            "A instalação deve ser revista por um eletricista pelo menos a cada cinco anos, ou antes se houver reforma, mudança de uso ou sinais de problema. " +
            "Guarde com este manual a planta elétrica, o quadro de cargas, o memorial de cálculo e as notas fiscais dos equipamentos do quadro.",
        ], ItensOpcionais:
        [
            "Mensal: teste do botão do DR.",
            "Anual: olhar o indicador do DPS; conferir se as tampas das caixas e do quadro estão no lugar e se a identificação dos circuitos está legível.",
            "A cada cinco anos: inspeção completa por profissional (apertos, aquecimentos, isolação, aterramento).",
        ]));

        secoes.Add(new SecaoDoManual("Identificação",
        [
            $"Documento gerado pelo AutoEletrica em {DateTime.Now:dd/MM/yyyy} a partir do projeto {r.Nome}. Circuitos e disjuntores estão identificados no quadro com os mesmos números deste manual; " +
            "a advertência padrão da NBR 5410 (6.5.4.10) está fixada na porta do quadro.",
        ]));

        return secoes;
    }
}
