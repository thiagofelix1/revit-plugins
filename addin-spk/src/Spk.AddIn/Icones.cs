using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using FlowDirection = System.Windows.FlowDirection;
using FontFamily = System.Windows.Media.FontFamily;
using FormattedText = System.Windows.Media.FormattedText;
using Freezable = System.Windows.Freezable;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace Spk.AddIn;

/// <summary>
///     Ícones da faixa de opções, um por botão, desenhados num quadro de 32×32 e rasterizados no tamanho pedido.
///     Grafite no contorno, vermelho no que o botão faz, cinza de apoio; o azul só onde há água e as cores do Cores da rede
///     só nele.
/// </summary>
static class Icones
{
    static readonly Brush Vermelho = Congelado(new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)));
    static readonly Brush Grafite = Congelado(new SolidColorBrush(Color.FromRgb(0x37, 0x47, 0x4F)));
    static readonly Brush Cinza = Congelado(new SolidColorBrush(Color.FromRgb(0x90, 0xA4, 0xAE)));
    static readonly Brush Claro = Congelado(new SolidColorBrush(Color.FromRgb(0xEC, 0xEF, 0xF1)));
    static readonly Brush Azul = Congelado(new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5)));
    static readonly Brush Amarelo = Congelado(new SolidColorBrush(Color.FromRgb(0xF9, 0xA8, 0x25)));
    static readonly Brush Verde = Congelado(new SolidColorBrush(Color.FromRgb(0x43, 0xA0, 0x47)));
    static readonly Brush Branco = Brushes.White;

    /// <summary>
    ///     Rasteriza o desenho num bitmap de <paramref name="tamanho" /> unidades (o que o Revit pede) com o dobro de pixels,
    ///     para ficar nítido em tela de 150–200 %. O desenho é feito em unidades de tela: a escala é tamanho/32, não pixels/32
    ///     (com pixels/32 o desenho saía com o dobro do tamanho e só o canto de cima à esquerda aparecia no botão).
    /// </summary>
    public static ImageSource Gerar(Action<DrawingContext> desenhar, int tamanho)
    {
        const double escala = 2;
        const double margem = 1;
        var pixels = (int)(tamanho * escala);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(tamanho / 32.0, tamanho / 32.0));
            var interno = (32 - 2 * margem) / 32.0;
            dc.PushTransform(new TranslateTransform(margem, margem));
            dc.PushTransform(new ScaleTransform(interno, interno));
            desenhar(dc);
            dc.Pop();
            dc.Pop();
            dc.Pop();
        }

        var bitmap = new RenderTargetBitmap(pixels, pixels, 96 * escala, 96 * escala, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    // ---------------------------------------------------------------- Projeto

    /// <summary>Padrão do escritório: a prancha de outro projeto passa o carimbo para esta.</summary>
    public static void Padrao(DrawingContext dc)
    {
        dc.DrawRectangle(Claro, Caneta(Cinza, 1.8), new Rect(2, 2, 16, 20));
        dc.DrawRectangle(Cinza, null, new Rect(10, 16, 6, 4));
        dc.DrawRectangle(Branco, Caneta(Grafite, 2.2), new Rect(13, 9, 17, 21));
        dc.DrawRectangle(Vermelho, null, new Rect(21, 23, 7, 5));
        dc.DrawGeometry(null, Caneta(Vermelho, 2.2), Geometry.Parse("M 5,25 L 5,27 L 9,27"));
        dc.DrawGeometry(Vermelho, null, Geometry.Parse("M 8.5,24 L 12.5,27 L 8.5,30 Z"));
    }

    /// <summary>Dados do projeto: prancheta com os critérios e a gota da demanda de água.</summary>
    public static void DadosDoProjeto(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(Branco, Caneta(Grafite, 2.2), new Rect(3, 4, 18, 25), 2, 2);
        dc.DrawRoundedRectangle(Grafite, null, new Rect(8, 2, 8, 5), 1.5, 1.5);
        foreach (var y in new[] { 12.0, 17.0, 22.0 })
        {
            dc.DrawEllipse(Vermelho, null, new Point(7.5, y), 1.5, 1.5);
            dc.DrawLine(Caneta(Grafite, 1.8), new Point(11, y), new Point(17, y));
        }
        dc.DrawGeometry(Azul, Caneta(Grafite, 1.4), Geometry.Parse("M 25,13 C 25,13 19.5,19.5 19.5,23 A 5.5,5.5 0 0 0 30.5,23 C 30.5,19.5 25,13 25,13 Z"));
    }

    /// <summary>Famílias: o bico com a etiqueta do fator K confirmado.</summary>
    public static void Familias(DrawingContext dc)
    {
        Tubo(dc, "M 3,4 L 21,4", 4.4);
        Tubo(dc, "M 11,4 L 11,10", 3.6);
        Bico(dc, 11, 15, 1.35);
        dc.DrawRoundedRectangle(Vermelho, null, new Rect(16, 17, 14, 13), 2.5, 2.5);
        Texto(dc, "K", 11, Branco, new Point(19.2, 16.6), negrito: true);
    }

    /// <summary>Arquitetura: o prédio vinculado, com as janelas e a viga que o SPK lê.</summary>
    public static void Arquitetura(DrawingContext dc)
    {
        dc.DrawLine(Caneta(Grafite, 3), new Point(2, 30), new Point(30, 30));
        dc.DrawRectangle(Branco, Caneta(Grafite, 2.2), new Rect(6, 3, 20, 27));
        foreach (var y in new[] { 7.0, 18.0 })
            foreach (var x in new[] { 9.5, 18.5 })
                dc.DrawRectangle(Claro, Caneta(Cinza, 1.2), new Rect(x, y, 4, 4.5));
        dc.DrawRectangle(Vermelho, null, new Rect(6, 13.5, 20, 2.6));
        dc.DrawRectangle(Grafite, null, new Rect(13.5, 24, 5, 6));
    }

    // ---------------------------------------------------------------- Rede

    /// <summary>Gerar rede: o ambiente com a subgeral no meio e os ramais saindo para os dois lados.</summary>
    public static void GerarRede(DrawingContext dc)
    {
        dc.DrawRectangle(Branco, Caneta(Grafite, 2.2), new Rect(2, 2, 28, 28));
        Tubo(dc, "M 16,30 L 16,6", 4.6);
        var ramal = Caneta(Vermelho, 2.2);
        foreach (var y in new[] { 10.0, 21.0 })
        {
            dc.DrawLine(ramal, new Point(6, y), new Point(26, y));
            foreach (var x in new[] { 7.0, 25.0 }) dc.DrawEllipse(Vermelho, null, new Point(x, y + 3.5), 2.2, 2.2);
        }
    }

    /// <summary>Bicos por ambiente: a planta do ambiente com a grade de bicos.</summary>
    public static void BicosPorAmbiente(DrawingContext dc)
    {
        dc.DrawRectangle(Branco, Caneta(Grafite, 2.4), new Rect(2, 4, 28, 24));
        var guia = Caneta(Cinza, 1.2);
        guia.DashStyle = new DashStyle([2, 2], 0);
        dc.DrawLine(guia, new Point(16, 4), new Point(16, 28));
        dc.DrawLine(guia, new Point(2, 16), new Point(30, 16));
        foreach (var x in new[] { 9.0, 23.0 })
            foreach (var y in new[] { 10.0, 22.0 })
                dc.DrawEllipse(Vermelho, null, new Point(x, y), 3, 3);
    }

    /// <summary>Traçar ramais: da subgeral saem os ramais até as fileiras de bicos.</summary>
    public static void TracarRamais(DrawingContext dc)
    {
        Tubo(dc, "M 5,2 L 5,30", 5);
        var ramal = Caneta(Vermelho, 2.4);
        foreach (var y in new[] { 9.0, 22.0 })
        {
            dc.DrawLine(ramal, new Point(5, y), new Point(29, y));
            foreach (var x in new[] { 15.0, 25.0 })
            {
                dc.DrawLine(Caneta(Vermelho, 1.6), new Point(x, y), new Point(x, y + 3.5));
                dc.DrawEllipse(Vermelho, null, new Point(x, y + 5), 2.2, 2.2);
            }
        }
    }

    /// <summary>Ligar bicos: o ramal com as descidas até os bicos.</summary>
    public static void LigarBicos(DrawingContext dc)
    {
        Tubo(dc, "M 2,5 L 30,5", 5);
        var descida = Caneta(Vermelho, 2.4);
        dc.DrawLine(descida, new Point(9, 5), new Point(9, 17));
        dc.DrawGeometry(null, descida, Geometry.Parse("M 21,5 L 21,11 L 25,11 L 25,17"));
        dc.DrawEllipse(Vermelho, null, new Point(9, 5), 2, 2);
        dc.DrawEllipse(Vermelho, null, new Point(21, 5), 2, 2);
        Bico(dc, 9, 23, 1.15, comCano: false);
        Bico(dc, 25, 23, 1.15, comCano: false);
    }

    /// <summary>Ligar à geral: as redes das salas descem até a geral do corredor.</summary>
    public static void LigarAGeral(DrawingContext dc)
    {
        foreach (var x in new[] { 2.0, 18.0 })
        {
            dc.DrawRectangle(Branco, Caneta(Grafite, 2), new Rect(x, 2, 12, 11));
            dc.DrawLine(Caneta(Cinza, 1.6), new Point(x + 3, 7.5), new Point(x + 9, 7.5));
        }
        Tubo(dc, "M 2,26 L 30,26", 5.5);
        var ligacao = Caneta(Vermelho, 2.6);
        foreach (var x in new[] { 8.0, 24.0 })
        {
            dc.DrawLine(ligacao, new Point(x, 13), new Point(x, 23));
            dc.DrawRectangle(Vermelho, null, new Rect(x - 2.5, 23, 5, 6));
        }
    }

    /// <summary>Inserir bico: o bico novo no ramal, com o sinal de acrescentar.</summary>
    public static void InserirBico(DrawingContext dc)
    {
        BicoNoRamal(dc);
        Selo(dc, "M 24,20.5 L 24,27.5 M 20.5,24 L 27.5,24");
    }

    /// <summary>Apagar bico: o bico com o X de tirar.</summary>
    public static void ApagarBico(DrawingContext dc)
    {
        BicoNoRamal(dc);
        Selo(dc, "M 21.5,21.5 L 26.5,26.5 M 26.5,21.5 L 21.5,26.5");
    }

    /// <summary>Mover bico: o bico sai do lugar antigo (cinza) e vai para o novo pela seta.</summary>
    public static void MoverBico(DrawingContext dc)
    {
        Tubo(dc, "M 2,5 L 30,5", 4.6);
        var antigo = Caneta(Cinza, 1.6);
        antigo.DashStyle = new DashStyle([1.5, 1.5], 0);
        dc.DrawLine(antigo, new Point(7, 7), new Point(7, 15));
        Bico(dc, 7, 20, 1.15, comCano: false, cor: Claro, contorno: Cinza);
        dc.DrawLine(Caneta(Grafite, 2.2), new Point(24, 7), new Point(24, 15));
        Bico(dc, 24, 20, 1.15, comCano: false);
        dc.DrawLine(Caneta(Vermelho, 2.4), new Point(9, 28), new Point(19, 28));
        dc.DrawGeometry(Vermelho, null, Geometry.Parse("M 18,24.5 L 24,28 L 18,31.5 Z"));
    }

    /// <summary>Ligar tubos: o tubo que chega e o de destino, unidos pelo tê.</summary>
    public static void LigarTubos(DrawingContext dc)
    {
        Tubo(dc, "M 24,2 L 24,30", 5.5);
        Tubo(dc, "M 2,16 L 14,16", 5.5);
        dc.DrawLine(Caneta(Vermelho, 3), new Point(14, 16), new Point(21, 16));
        dc.DrawRectangle(Vermelho, Caneta(Grafite, 1.2), new Rect(19.5, 12, 9, 8));
    }

    /// <summary>Conferir na planta: o alcance do bico desenhado e o ponto sem cobertura marcado.</summary>
    public static void ConferirNaPlanta(DrawingContext dc)
    {
        var alcance = Caneta(Cinza, 1.6);
        alcance.DashStyle = new DashStyle([2, 1.5], 0);
        dc.DrawEllipse(Claro, alcance, new Point(13, 13), 11, 11);
        dc.DrawEllipse(Grafite, null, new Point(13, 13), 3, 3);
        dc.DrawEllipse(Branco, Caneta(Vermelho, 2.8), new Point(25, 25), 5, 5);
        dc.DrawEllipse(Vermelho, null, new Point(25, 25), 1.6, 1.6);
    }

    /// <summary>Limpar conferência: a borracha sobre a marca da conferência.</summary>
    public static void LimparConferencia(DrawingContext dc)
    {
        var alcance = Caneta(Cinza, 1.6);
        alcance.DashStyle = new DashStyle([2, 1.5], 0);
        dc.DrawEllipse(null, alcance, new Point(12, 12), 9.5, 9.5);
        dc.DrawEllipse(null, Caneta(Cinza, 2.2), new Point(12, 12), 3.5, 3.5);
        dc.PushTransform(new RotateTransform(-40, 21, 22));
        dc.DrawRoundedRectangle(Branco, Caneta(Grafite, 2), new Rect(12, 17, 18, 10), 2, 2);
        dc.DrawRoundedRectangle(Vermelho, null, new Rect(22, 18, 7, 8), 1.2, 1.2);
        dc.Pop();
    }

    /// <summary>Malha retangular: tubos em grade, já ligados, com os bicos nos cruzamentos.</summary>
    public static void MalhaRetangular(DrawingContext dc)
    {
        foreach (var y in new[] { 9.0, 23.0 }) Tubo(dc, $"M 2,{F(y)} L 30,{F(y)}", 3.8);
        foreach (var x in new[] { 9.0, 23.0 }) Tubo(dc, $"M {F(x)},2 L {F(x)},30", 3.8);
        foreach (var x in new[] { 9.0, 23.0 })
            foreach (var y in new[] { 9.0, 23.0 })
                dc.DrawEllipse(Vermelho, Caneta(Branco, 1), new Point(x, y), 3.2, 3.2);
    }

    /// <summary>Dados do esboço: o esquadro com as medidas da malha.</summary>
    public static void DadosDoEsboco(DrawingContext dc)
    {
        dc.DrawGeometry(Branco, Caneta(Grafite, 2.2), Geometry.Parse("M 3,29 L 3,3 L 29,29 Z M 8,24 L 8,15 L 17,24 Z"));
        var marca = Caneta(Grafite, 1.4);
        foreach (var x in new[] { 7.0, 11.0, 15.0, 19.0, 23.0 }) dc.DrawLine(marca, new Point(x, 29), new Point(x, 26.5));
        foreach (var y in new[] { 9.0, 13.0 }) dc.DrawLine(marca, new Point(3, y), new Point(5.5, y));
        foreach (var x in new[] { 22.0, 29.0 }) dc.DrawEllipse(Vermelho, null, new Point(x, 8), 2.4, 2.4);
        dc.DrawLine(Caneta(Vermelho, 1.6), new Point(22, 13), new Point(29, 13));
        dc.DrawLine(Caneta(Vermelho, 1.6), new Point(22, 11.5), new Point(22, 14.5));
        dc.DrawLine(Caneta(Vermelho, 1.6), new Point(29, 11.5), new Point(29, 14.5));
    }

    /// <summary>Rede de exemplo: o galpão de teste com os bicos já postos.</summary>
    public static void RedeDeExemplo(DrawingContext dc)
    {
        dc.DrawGeometry(Branco, Caneta(Grafite, 2.2), Geometry.Parse("M 2,14 L 16,4 L 30,14 L 30,29 L 2,29 Z"));
        dc.DrawLine(Caneta(Vermelho, 2), new Point(6, 17), new Point(26, 17));
        foreach (var x in new[] { 8.0, 16.0, 24.0 })
        {
            dc.DrawEllipse(Vermelho, null, new Point(x, 20.5), 2.2, 2.2);
            dc.DrawEllipse(Cinza, null, new Point(x, 26), 1.6, 1.6);
        }
    }

    // ---------------------------------------------------------------- Cálculo

    /// <summary>Marcar setor: a descida da VGA com a bandeira do nome do setor.</summary>
    public static void MarcarSetor(DrawingContext dc)
    {
        Tubo(dc, "M 8,30 L 8,13 L 30,13", 5);
        dc.DrawLine(Caneta(Grafite, 1.8), new Point(14, 2), new Point(14, 13));
        dc.DrawGeometry(Vermelho, null, Geometry.Parse("M 14.5,2 L 29,5.5 L 14.5,9.5 Z"));
        dc.DrawEllipse(Vermelho, Caneta(Branco, 1.2), new Point(8, 26), 3.4, 3.4);
    }

    /// <summary>Verificar modelo: a lupa com o visto sobre o modelo.</summary>
    public static void Verificar(DrawingContext dc)
    {
        dc.DrawLine(Caneta(Grafite, 5), new Point(20, 20), new Point(28.5, 28.5));
        dc.DrawEllipse(Branco, Caneta(Grafite, 2.6), new Point(13, 13), 10, 10);
        dc.DrawGeometry(null, Caneta(Vermelho, 3), Geometry.Parse("M 8,13.5 L 11.5,17 L 18.5,9"));
    }

    /// <summary>Diâmetros: o tubo que afina a cada derivação, como o ramal dimensionado pela tabela.</summary>
    public static void Diametros(DrawingContext dc)
    {
        Tubo(dc, "M 2,16 L 12,16", 10);
        dc.DrawLine(Caneta(Vermelho, 6.5, PenLineCap.Flat), new Point(12, 16), new Point(21, 16));
        dc.DrawLine(Caneta(Vermelho, 3.5, PenLineCap.Flat), new Point(21, 16), new Point(30, 16));
        var cota = Caneta(Grafite, 1.4);
        dc.DrawLine(cota, new Point(12, 6), new Point(12, 26));
        dc.DrawLine(cota, new Point(21, 8), new Point(21, 24));
    }

    /// <summary>Calcular: o manômetro com o ponteiro na pressão pedida.</summary>
    public static void Calcular(DrawingContext dc)
    {
        Tubo(dc, "M 16,25 L 16,31", 5);
        dc.DrawEllipse(Branco, Caneta(Grafite, 2.4), new Point(16, 14), 12.5, 12.5);
        var marca = Caneta(Grafite, 1.6);
        foreach (var angulo in new[] { 150.0, 195.0, 240.0, 285.0, 330.0, 375.0 })
        {
            var r = angulo * Math.PI / 180;
            dc.DrawLine(marca, new Point(16 + 8.5 * Math.Cos(r), 14 + 8.5 * Math.Sin(r)), new Point(16 + 11 * Math.Cos(r), 14 + 11 * Math.Sin(r)));
        }
        dc.DrawLine(Caneta(Vermelho, 2.8), new Point(16, 14), new Point(22.5, 7));
        dc.DrawEllipse(Grafite, null, new Point(16, 14), 2.4, 2.4);
    }

    // ---------------------------------------------------------------- Resultados

    /// <summary>Memorial: o documento com o selo de conferido.</summary>
    public static void Memorial(DrawingContext dc)
    {
        dc.DrawGeometry(Branco, Caneta(Grafite, 2.2), Geometry.Parse("M 4,2 L 18,2 L 25,9 L 25,29 L 4,29 Z"));
        dc.DrawGeometry(Claro, Caneta(Grafite, 1.6), Geometry.Parse("M 18,2 L 18,9 L 25,9"));
        var linha = Caneta(Grafite, 1.6);
        foreach (var y in new[] { 13.0, 17.0, 21.0 }) dc.DrawLine(linha, new Point(8, y), new Point(19, y));
        dc.DrawEllipse(Vermelho, Caneta(Branco, 1.5), new Point(24, 24), 6, 6);
        dc.DrawGeometry(null, Caneta(Branco, 2), Geometry.Parse("M 21,24 L 23.2,26.2 L 27,22"));
    }

    /// <summary>Folhas: a prancha com a planta do setor, os bicos e o carimbo.</summary>
    public static void Folhas(DrawingContext dc)
    {
        dc.DrawRectangle(Cinza, null, new Rect(3.5, 6.5, 27, 21));
        dc.DrawRectangle(Branco, Caneta(Grafite, 2), new Rect(2, 5, 27, 21));
        var planta = Caneta(Cinza, 1.4);
        dc.DrawRectangle(null, planta, new Rect(5, 8, 13, 15));
        foreach (var x in new[] { 8.5, 14.5 })
            foreach (var y in new[] { 12.0, 19.0 })
                dc.DrawEllipse(Vermelho, null, new Point(x, y), 1.6, 1.6);
        dc.DrawRectangle(Claro, Caneta(Vermelho, 1.6), new Rect(20, 17, 7, 6.5));
        dc.DrawLine(Caneta(Vermelho, 1.2), new Point(20, 20), new Point(27, 20));
    }

    /// <summary>Cores da rede: a paleta com as cores da legenda.</summary>
    public static void CoresDaRede(DrawingContext dc) => Paleta(dc, [Azul, Amarelo, Vermelho, Verde]);

    /// <summary>Tirar cores: a paleta apagada e o X de desfazer.</summary>
    public static void TirarCores(DrawingContext dc)
    {
        Paleta(dc, [Cinza, Claro, Cinza, Claro]);
        Selo(dc, "M 21.5,21.5 L 26.5,26.5 M 26.5,21.5 L 21.5,26.5");
    }

    /// <summary>Lista de material: a prancheta com os itens conferidos.</summary>
    public static void ListaDeMaterial(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(Branco, Caneta(Grafite, 2.2), new Rect(5, 4, 22, 26), 2, 2);
        dc.DrawRoundedRectangle(Grafite, null, new Rect(11, 2, 10, 5), 1.5, 1.5);
        foreach (var y in new[] { 12.0, 18.0, 24.0 })
        {
            dc.DrawGeometry(null, Caneta(Vermelho, 2), Geometry.Parse($"M 8.5,{F(y)} L 10.5,{F(y + 2)} L 14,{F(y - 2.5)}"));
            dc.DrawLine(Caneta(Grafite, 1.8), new Point(16, y), new Point(23.5, y));
        }
    }

    // ---------------------------------------------------------------- Ajuda e Avançado

    /// <summary>Guia: interrogação no círculo.</summary>
    public static void Guia(DrawingContext dc)
    {
        dc.DrawEllipse(Vermelho, null, new Point(16, 16), 14, 14);
        dc.DrawGeometry(null, Caneta(Branco, 3.4), Geometry.Parse("M 11.5,12 A 4.5,4.5 0 1 1 18.5,15.6 C 16.8,16.8 16,17.6 16,20"));
        dc.DrawEllipse(Branco, null, new Point(16, 24.6), 2, 2);
    }

    /// <summary>Avançado: a engrenagem das ferramentas de desenvolvimento.</summary>
    public static void Avancado(DrawingContext dc)
    {
        for (var k = 0; k < 8; k++)
        {
            dc.PushTransform(new RotateTransform(k * 45, 16, 16));
            dc.DrawRoundedRectangle(Grafite, null, new Rect(13, 2, 6, 7), 1, 1);
            dc.Pop();
        }
        dc.DrawEllipse(Grafite, null, new Point(16, 16), 10.5, 10.5);
        dc.DrawEllipse(Branco, null, new Point(16, 16), 5.5, 5.5);
        dc.DrawEllipse(Vermelho, null, new Point(16, 16), 3, 3);
    }

    /// <summary>Exemplo: rodar o cálculo do galpão fictício do motor.</summary>
    public static void Exemplo(DrawingContext dc)
    {
        dc.DrawGeometry(Vermelho, Caneta(Vermelho, 2), Geometry.Parse("M 9,4 L 28,16 L 9,28 Z"));
    }

    // ---------------------------------------------------------------- peças

    /// <summary>Tubo: traço grafite largo com o miolo claro, para ler como tubo em qualquer escala.</summary>
    static void Tubo(DrawingContext dc, string caminho, double largura)
    {
        var geometria = Geometry.Parse(caminho);
        dc.DrawGeometry(null, Caneta(Grafite, largura, PenLineCap.Flat), geometria);
        dc.DrawGeometry(null, Caneta(Claro, largura * 0.45, PenLineCap.Flat), geometria);
    }

    /// <summary>
    ///     Bico pendente com o centro do corpo em (x, y): o niple em cima, o corpo e o defletor embaixo. No tamanho 1 ocupa
    ///     cerca de 9 × 11.
    /// </summary>
    static void Bico(DrawingContext dc, double x, double y, double tamanho = 1, bool comCano = true, Brush? cor = null, Brush? contorno = null)
    {
        dc.PushTransform(new TranslateTransform(x, y));
        dc.PushTransform(new ScaleTransform(tamanho, tamanho));
        var traco = contorno ?? Grafite;
        if (comCano) dc.DrawRectangle(traco, null, new Rect(-1.4, -6.5, 2.8, 4));
        dc.DrawGeometry(cor ?? Vermelho, Caneta(traco, 1.2), Geometry.Parse("M -3.2,-2.5 L 3.2,-2.5 L 1.8,2 L -1.8,2 Z"));
        dc.DrawLine(Caneta(traco, 1.8), new Point(-4.5, 3.6), new Point(4.5, 3.6));
        dc.Pop();
        dc.Pop();
    }

    /// <summary>Ramal no alto, a descida e o bico: a base de Inserir e Apagar bico.</summary>
    static void BicoNoRamal(DrawingContext dc)
    {
        Tubo(dc, "M 2,5 L 22,5", 4.6);
        dc.DrawLine(Caneta(Grafite, 2.2), new Point(11, 7), new Point(11, 13));
        Bico(dc, 11, 18, 1.3, comCano: false);
    }

    /// <summary>Selo vermelho no canto de baixo à direita com o sinal em branco (+, X).</summary>
    static void Selo(DrawingContext dc, string sinal)
    {
        dc.DrawEllipse(Vermelho, Caneta(Branco, 1.5), new Point(24, 24), 6.5, 6.5);
        dc.DrawGeometry(null, Caneta(Branco, 2.2), Geometry.Parse(sinal));
    }

    /// <summary>Paleta de pintor com quatro tintas.</summary>
    static void Paleta(DrawingContext dc, Brush[] tintas)
    {
        dc.DrawGeometry(Branco, Caneta(Grafite, 2.2), Geometry.Parse(
            "M 16,3 C 7,3 2,9 2,16 C 2,23 8,29 15,29 C 18,29 19,27 18,25 C 17,23 18,21 21,21 L 24,21 C 28,21 30,18 30,14 C 30,8 24,3 16,3 Z"));
        var lugares = new[] { new Point(8.5, 13), new Point(14.5, 8.5), new Point(21.5, 9.5), new Point(9, 21) };
        for (var k = 0; k < lugares.Length; k++)
            dc.DrawEllipse(tintas[k], Caneta(Grafite, 1), lugares[k], 2.8, 2.8);
    }

    static void Texto(DrawingContext dc, string texto, double tamanho, Brush cor, Point origem, bool negrito = false)
    {
        var fonte = new Typeface(new FontFamily("Segoe UI"), System.Windows.FontStyles.Normal,
            negrito ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.SemiBold, System.Windows.FontStretches.Normal);
        var formatado = new FormattedText(texto, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, fonte, tamanho, cor, 1.0);
        dc.DrawText(formatado, origem);
    }

    static string F(double valor) => valor.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Caneta sem congelar: algumas recebem o tracejado depois de criadas.</summary>
    static Pen Caneta(Brush cor, double espessura, PenLineCap ponta = PenLineCap.Round) =>
        new(cor, espessura) { StartLineCap = ponta, EndLineCap = ponta, LineJoin = PenLineJoin.Round };

    static T Congelado<T>(T objeto) where T : Freezable
    {
        objeto.Freeze();
        return objeto;
    }
}
