using Eletrica.Motor.Modelo;

namespace Eletrica.Motor.Normas;

/// <summary>
/// Tabelas 36 a 39 da ABNT NBR 5410:2004 (6.2.5): capacidade de condução de corrente em ampères por seção, método de
/// referência, isolação, material e número de condutores carregados. Referência 30 °C no ar e 20 °C no solo.
/// Transcritas do PDF e conferidas coluna a coluna com a IEC 60364-5-52 (Tabelas B.52.2 a B.52.5 e B.52.10 a B.52.13),
/// de onde as tabelas brasileiras vêm. Seções em mm².
/// </summary>
public static class Nbr5410Capacidades
{
    public const string Documento = "ABNT NBR 5410:2004";

    /// <summary>As 24 seções das tabelas, na ordem das linhas.</summary>
    public static readonly double[] Secoes =
        [0.5, 0.75, 1, 1.5, 2.5, 4, 6, 10, 16, 25, 35, 50, 70, 95, 120, 150, 185, 240, 300, 400, 500, 630, 800, 1000];

    /// <summary>Seções a partir das quais há valores para alumínio (6.2.3.7: alumínio só de 16 mm² para cima).</summary>
    const int PrimeiraLinhaAluminio = 8;

    // ---------- Tabela 36: PVC 70 °C, métodos A1, A2, B1, B2, C, D ----------
    static readonly double[] T36CuA1_2 = [7, 9, 11, 14.5, 19.5, 26, 34, 46, 61, 80, 99, 119, 151, 182, 210, 240, 273, 321, 367, 438, 502, 578, 669, 767];
    static readonly double[] T36CuA1_3 = [7, 9, 10, 13.5, 18, 24, 31, 42, 56, 73, 89, 108, 136, 164, 188, 216, 245, 286, 328, 390, 447, 514, 593, 679];
    static readonly double[] T36CuA2_2 = [7, 9, 11, 14, 18.5, 25, 32, 43, 57, 75, 92, 110, 139, 167, 192, 219, 248, 291, 334, 398, 456, 526, 609, 698];
    static readonly double[] T36CuA2_3 = [7, 9, 10, 13, 17.5, 23, 29, 39, 52, 68, 83, 99, 125, 150, 172, 196, 223, 261, 298, 355, 406, 467, 540, 618];
    static readonly double[] T36CuB1_2 = [9, 11, 14, 17.5, 24, 32, 41, 57, 76, 101, 125, 151, 192, 232, 269, 309, 353, 415, 477, 571, 656, 758, 881, 1012];
    static readonly double[] T36CuB1_3 = [8, 10, 12, 15.5, 21, 28, 36, 50, 68, 89, 110, 134, 171, 207, 239, 275, 314, 370, 426, 510, 587, 678, 788, 906];
    static readonly double[] T36CuB2_2 = [9, 11, 13, 16.5, 23, 30, 38, 52, 69, 90, 111, 133, 168, 201, 232, 265, 300, 351, 401, 477, 545, 626, 723, 827];
    static readonly double[] T36CuB2_3 = [8, 10, 12, 15, 20, 27, 34, 46, 62, 80, 99, 118, 149, 179, 206, 236, 268, 313, 358, 425, 486, 559, 645, 738];
    static readonly double[] T36CuC_2 = [10, 13, 15, 19.5, 27, 36, 46, 63, 85, 112, 138, 168, 213, 258, 299, 344, 392, 461, 530, 634, 729, 843, 978, 1125];
    static readonly double[] T36CuC_3 = [9, 11, 14, 17.5, 24, 32, 41, 57, 76, 96, 119, 144, 184, 223, 259, 299, 341, 403, 464, 557, 642, 743, 865, 996];
    static readonly double[] T36CuD_2 = [12, 15, 18, 22, 29, 38, 47, 63, 81, 104, 125, 148, 183, 216, 246, 278, 312, 361, 408, 478, 540, 614, 700, 792];
    static readonly double[] T36CuD_3 = [10, 12, 15, 18, 24, 31, 39, 52, 67, 86, 103, 122, 151, 179, 203, 230, 258, 297, 336, 394, 445, 506, 577, 652];

    static readonly double[] T36AlA1_2 = [48, 63, 77, 93, 118, 142, 164, 189, 215, 252, 289, 345, 396, 456, 529, 607];
    static readonly double[] T36AlA1_3 = [43, 57, 70, 84, 107, 129, 149, 170, 194, 227, 261, 311, 356, 410, 475, 544];
    static readonly double[] T36AlA2_2 = [44, 58, 71, 86, 108, 130, 150, 172, 195, 229, 263, 314, 360, 416, 482, 552];
    static readonly double[] T36AlA2_3 = [41, 53, 65, 78, 98, 118, 135, 155, 176, 207, 237, 283, 324, 373, 432, 495];
    static readonly double[] T36AlB1_2 = [60, 79, 97, 118, 150, 181, 210, 241, 275, 324, 372, 446, 512, 592, 687, 790];
    static readonly double[] T36AlB1_3 = [53, 70, 86, 104, 133, 161, 186, 214, 245, 288, 331, 397, 456, 527, 612, 704];
    static readonly double[] T36AlB2_2 = [54, 71, 86, 104, 131, 157, 181, 206, 234, 274, 313, 372, 425, 488, 563, 643];
    static readonly double[] T36AlB2_3 = [48, 62, 77, 92, 116, 139, 160, 183, 208, 243, 278, 331, 378, 435, 502, 574];
    static readonly double[] T36AlC_2 = [66, 83, 103, 125, 160, 195, 226, 261, 298, 352, 406, 488, 563, 653, 761, 878];
    static readonly double[] T36AlC_3 = [59, 73, 90, 110, 140, 170, 197, 227, 259, 305, 351, 422, 486, 562, 654, 753];
    static readonly double[] T36AlD_2 = [62, 80, 96, 113, 140, 166, 189, 213, 240, 277, 313, 366, 414, 471, 537, 607];
    static readonly double[] T36AlD_3 = [52, 66, 80, 94, 117, 138, 157, 178, 200, 230, 260, 305, 345, 391, 446, 505];

    // ---------- Tabela 37: EPR/XLPE 90 °C, métodos A1, A2, B1, B2, C, D ----------
    static readonly double[] T37CuA1_2 = [10, 12, 15, 19, 26, 35, 45, 61, 81, 106, 131, 158, 200, 241, 278, 318, 362, 424, 486, 579, 664, 765, 885, 1014];
    static readonly double[] T37CuA1_3 = [9, 11, 13, 17, 23, 31, 40, 54, 73, 95, 117, 141, 179, 216, 249, 285, 324, 380, 435, 519, 595, 685, 792, 908];
    static readonly double[] T37CuA2_2 = [10, 12, 14, 18.5, 25, 33, 42, 57, 76, 99, 121, 145, 183, 220, 253, 290, 329, 386, 442, 527, 604, 696, 805, 923];
    static readonly double[] T37CuA2_3 = [9, 11, 13, 16.5, 22, 30, 38, 51, 68, 89, 109, 130, 164, 197, 227, 259, 295, 346, 396, 472, 541, 623, 721, 826];
    static readonly double[] T37CuB1_2 = [12, 15, 18, 23, 31, 42, 54, 75, 100, 133, 164, 198, 253, 306, 354, 407, 464, 546, 628, 751, 864, 998, 1158, 1332];
    static readonly double[] T37CuB1_3 = [10, 13, 16, 20, 28, 37, 48, 66, 88, 117, 144, 175, 222, 269, 312, 358, 408, 481, 553, 661, 760, 879, 1020, 1173];
    static readonly double[] T37CuB2_2 = [11, 15, 17, 22, 30, 40, 51, 69, 91, 119, 146, 175, 221, 265, 305, 349, 395, 462, 529, 628, 718, 825, 952, 1088];
    static readonly double[] T37CuB2_3 = [10, 13, 15, 19.5, 26, 35, 44, 60, 80, 105, 128, 154, 194, 233, 268, 307, 348, 407, 465, 552, 631, 725, 837, 957];
    static readonly double[] T37CuC_2 = [12, 16, 19, 24, 33, 45, 58, 80, 107, 138, 171, 209, 269, 328, 382, 441, 506, 599, 693, 835, 966, 1122, 1311, 1515];
    static readonly double[] T37CuC_3 = [11, 14, 17, 22, 30, 40, 52, 71, 96, 119, 147, 179, 229, 278, 322, 371, 424, 500, 576, 692, 797, 923, 1074, 1237];
    static readonly double[] T37CuD_2 = [14, 18, 21, 26, 34, 44, 56, 73, 95, 121, 146, 173, 213, 252, 287, 324, 363, 419, 474, 555, 627, 711, 811, 916];
    static readonly double[] T37CuD_3 = [12, 15, 17, 22, 29, 37, 46, 61, 79, 101, 122, 144, 178, 211, 240, 271, 304, 351, 396, 464, 525, 596, 679, 767];

    static readonly double[] T37AlA1_2 = [64, 84, 103, 125, 158, 191, 220, 253, 288, 338, 387, 462, 530, 611, 708, 812];
    static readonly double[] T37AlA1_3 = [58, 76, 94, 113, 142, 171, 197, 226, 256, 300, 344, 409, 468, 538, 622, 712];
    static readonly double[] T37AlA2_2 = [60, 78, 96, 115, 145, 175, 201, 230, 262, 307, 352, 421, 483, 556, 644, 739];
    static readonly double[] T37AlA2_3 = [55, 71, 87, 104, 131, 157, 180, 206, 233, 273, 313, 372, 426, 490, 566, 648];
    static readonly double[] T37AlB1_2 = [79, 105, 130, 157, 200, 242, 281, 323, 368, 433, 499, 597, 687, 794, 922, 1061];
    static readonly double[] T37AlB1_3 = [71, 93, 116, 140, 179, 217, 251, 289, 330, 389, 447, 536, 617, 714, 830, 955];
    static readonly double[] T37AlB2_2 = [72, 94, 115, 138, 175, 210, 242, 277, 314, 368, 421, 500, 573, 658, 760, 870];
    static readonly double[] T37AlB2_3 = [64, 84, 103, 124, 156, 188, 216, 248, 281, 329, 377, 448, 513, 590, 682, 780];
    static readonly double[] T37AlC_2 = [84, 101, 126, 154, 198, 241, 280, 324, 371, 439, 508, 612, 707, 821, 958, 1108];
    static readonly double[] T37AlC_3 = [76, 90, 112, 136, 174, 211, 245, 283, 323, 382, 440, 529, 610, 707, 824, 950];
    static readonly double[] T37AlD_2 = [73, 93, 112, 132, 163, 193, 220, 249, 279, 322, 364, 426, 482, 547, 624, 706];
    static readonly double[] T37AlD_3 = [61, 78, 94, 112, 138, 164, 186, 210, 236, 272, 308, 361, 408, 464, 529, 598];

    // ---------- Tabela 38: PVC 70 °C, métodos E, F, G ----------
    static readonly double[] T38CuE_2 = [11, 14, 17, 22, 30, 40, 51, 70, 94, 119, 148, 180, 232, 282, 328, 379, 434, 514, 593, 715, 826, 958, 1118, 1292];
    static readonly double[] T38CuE_3 = [9, 12, 14, 18.5, 25, 34, 43, 60, 80, 101, 126, 153, 196, 238, 276, 319, 364, 430, 497, 597, 689, 798, 930, 1073];
    static readonly double[] T38CuF_2 = [11, 14, 17, 22, 31, 41, 53, 73, 99, 131, 162, 196, 251, 304, 352, 406, 463, 546, 629, 754, 868, 1005, 1169, 1346];
    static readonly double[] T38CuF_3 = [8, 11, 13, 17, 24, 33, 43, 60, 82, 110, 137, 167, 216, 264, 308, 356, 409, 485, 561, 656, 749, 855, 971, 1079];
    static readonly double[] T38CuFPlano_3 = [9, 11, 14, 18, 25, 34, 45, 63, 85, 114, 143, 174, 225, 275, 321, 372, 427, 507, 587, 689, 789, 905, 1119, 1296];
    static readonly double[] T38CuG_3 = [12, 16, 19, 24, 34, 45, 59, 81, 110, 146, 181, 219, 281, 341, 396, 456, 521, 615, 709, 852, 982, 1138, 1325, 1528];
    static readonly double[] T38CuGV_3 = [10, 13, 16, 21, 29, 39, 51, 71, 97, 130, 162, 197, 254, 311, 362, 419, 480, 569, 659, 795, 920, 1070, 1251, 1448];

    // Alumínio da Tabela 38 só até 240 mm² (as linhas finais do PDF saíram ilegíveis; completar na conferência).
    static readonly double[] T38AlE_2 = [73, 89, 111, 135, 173, 210, 244, 282, 322, 380];
    static readonly double[] T38AlE_3 = [61, 78, 96, 117, 150, 183, 212, 245, 280, 330];
    static readonly double[] T38AlF_2 = [73, 98, 122, 149, 192, 235, 273, 316, 363, 430];
    static readonly double[] T38AlF_3 = [62, 84, 105, 128, 166, 203, 237, 274, 315, 375];
    static readonly double[] T38AlFPlano_3 = [65, 87, 109, 133, 173, 212, 247, 287, 330, 392];
    static readonly double[] T38AlG_3 = [84, 112, 139, 169, 217, 265, 308, 356, 407, 482];
    static readonly double[] T38AlGV_3 = [73, 99, 124, 152, 196, 241, 282, 327, 376, 447];

    // ---------- Tabela 39: EPR/XLPE 90 °C, métodos E, F, G ----------
    static readonly double[] T39CuE_2 = [13, 17, 21, 26, 36, 49, 63, 86, 115, 149, 185, 225, 289, 352, 410, 473, 542, 641, 741, 892, 1030, 1196, 1396, 1613];
    static readonly double[] T39CuE_3 = [12, 15, 18, 23, 32, 42, 54, 75, 100, 127, 158, 192, 246, 298, 346, 399, 456, 538, 621, 745, 859, 995, 1159, 1336];
    static readonly double[] T39CuF_2 = [13, 17, 21, 27, 37, 50, 65, 90, 121, 161, 200, 242, 310, 377, 437, 504, 575, 679, 783, 940, 1083, 1254, 1460, 1683];
    static readonly double[] T39CuF_3 = [10, 13, 16, 21, 29, 40, 53, 74, 101, 135, 169, 207, 268, 328, 383, 444, 510, 607, 703, 823, 946, 1088, 1252, 1420];
    static readonly double[] T39CuFPlano_3 = [10, 14, 17, 22, 30, 42, 55, 77, 105, 141, 176, 216, 279, 342, 400, 464, 533, 634, 736, 868, 998, 1151, 1328, 1511];
    static readonly double[] T39CuG_3 = [15, 19, 23, 30, 41, 56, 73, 101, 137, 182, 226, 275, 353, 430, 500, 577, 661, 781, 902, 1085, 1253, 1454, 1696, 1958];
    static readonly double[] T39CuGV_3 = [12, 16, 19, 25, 35, 48, 63, 88, 120, 161, 201, 246, 318, 389, 454, 527, 605, 719, 833, 1008, 1169, 1362, 1595, 1849];

    static readonly double[] T39AlE_2 = [91, 108, 135, 164, 211, 257, 300, 346, 397, 470, 543, 654, 756, 879, 1026, 1186];
    static readonly double[] T39AlE_3 = [77, 97, 120, 146, 187, 227, 263, 304, 347, 409, 471, 566, 652, 755, 879, 1012];
    static readonly double[] T39AlF_2 = [90, 121, 150, 184, 237, 289, 337, 389, 447, 530, 613, 740, 856, 996, 1164, 1347];
    static readonly double[] T39AlF_3 = [76, 103, 129, 159, 206, 253, 296, 343, 395, 471, 547, 663, 770, 899, 1056, 1226];
    static readonly double[] T39AlFPlano_3 = [79, 107, 135, 165, 215, 264, 308, 358, 413, 492, 571, 694, 806, 942, 1106, 1285];
    static readonly double[] T39AlG_3 = [103, 138, 172, 210, 271, 332, 387, 448, 515, 611, 708, 856, 991, 1154, 1351, 1565];
    static readonly double[] T39AlGV_3 = [90, 122, 153, 188, 244, 300, 351, 408, 470, 561, 652, 792, 921, 1077, 1266, 1472];

    /// <summary>Capacidade de condução de corrente da seção, em A, nas condições de referência (sem fatores de correção).</summary>
    /// <param name="condutoresCarregados">2 ou 3 (Tabela 46; 4 cai na coluna de 3 com o fator 0,86 aplicado fora daqui).</param>
    public static double Capacidade(Isolacao isolacao, MaterialCondutor material, MetodoReferencia metodo, int condutoresCarregados, double secao)
    {
        var coluna = Coluna(isolacao, material, metodo, condutoresCarregados);
        var linha = Array.IndexOf(Secoes, secao);
        if (linha < 0) throw new ArgumentException($"Seção {secao} mm² não está nas Tabelas 36 a 39 da NBR 5410.");
        var primeira = material == MaterialCondutor.Cobre ? 0 : PrimeiraLinhaAluminio;
        var indice = linha - primeira;
        if (indice < 0 || indice >= coluna.Length)
            throw new ArgumentException($"Não há capacidade tabelada para {material} {secao} mm² em {metodo} ({Tabela(isolacao, metodo)}).");
        return coluna[indice];
    }

    /// <summary>Nome da tabela que vale para a combinação, para o memorial.</summary>
    public static string Tabela(Isolacao isolacao, MetodoReferencia metodo)
    {
        var pvc = isolacao == Isolacao.Pvc70;
        var arLivre = metodo is MetodoReferencia.E or MetodoReferencia.F or MetodoReferencia.FPlano or MetodoReferencia.G or MetodoReferencia.GVertical;
        return (pvc, arLivre) switch
        {
            (true, false) => "Tabela 36",
            (false, false) => "Tabela 37",
            (true, true) => "Tabela 38",
            _ => "Tabela 39",
        };
    }

    /// <summary>Seções tabeladas para o material, em ordem crescente.</summary>
    public static IEnumerable<double> SecoesDe(MaterialCondutor material) =>
        material == MaterialCondutor.Cobre ? Secoes : Secoes.Skip(PrimeiraLinhaAluminio);

    static double[] Coluna(Isolacao isolacao, MaterialCondutor material, MetodoReferencia metodo, int n)
    {
        if (n is not (2 or 3)) throw new ArgumentOutOfRangeException(nameof(n), "Número de condutores carregados deve ser 2 ou 3.");
        var pvc = isolacao == Isolacao.Pvc70;
        var cu = material == MaterialCondutor.Cobre;
        var dois = n == 2;
        return metodo switch
        {
            MetodoReferencia.A1 => (pvc, cu, dois) switch
            {
                (true, true, true) => T36CuA1_2, (true, true, false) => T36CuA1_3, (true, false, true) => T36AlA1_2, (true, false, false) => T36AlA1_3,
                (false, true, true) => T37CuA1_2, (false, true, false) => T37CuA1_3, (false, false, true) => T37AlA1_2, _ => T37AlA1_3,
            },
            MetodoReferencia.A2 => (pvc, cu, dois) switch
            {
                (true, true, true) => T36CuA2_2, (true, true, false) => T36CuA2_3, (true, false, true) => T36AlA2_2, (true, false, false) => T36AlA2_3,
                (false, true, true) => T37CuA2_2, (false, true, false) => T37CuA2_3, (false, false, true) => T37AlA2_2, _ => T37AlA2_3,
            },
            MetodoReferencia.B1 => (pvc, cu, dois) switch
            {
                (true, true, true) => T36CuB1_2, (true, true, false) => T36CuB1_3, (true, false, true) => T36AlB1_2, (true, false, false) => T36AlB1_3,
                (false, true, true) => T37CuB1_2, (false, true, false) => T37CuB1_3, (false, false, true) => T37AlB1_2, _ => T37AlB1_3,
            },
            MetodoReferencia.B2 => (pvc, cu, dois) switch
            {
                (true, true, true) => T36CuB2_2, (true, true, false) => T36CuB2_3, (true, false, true) => T36AlB2_2, (true, false, false) => T36AlB2_3,
                (false, true, true) => T37CuB2_2, (false, true, false) => T37CuB2_3, (false, false, true) => T37AlB2_2, _ => T37AlB2_3,
            },
            MetodoReferencia.C => (pvc, cu, dois) switch
            {
                (true, true, true) => T36CuC_2, (true, true, false) => T36CuC_3, (true, false, true) => T36AlC_2, (true, false, false) => T36AlC_3,
                (false, true, true) => T37CuC_2, (false, true, false) => T37CuC_3, (false, false, true) => T37AlC_2, _ => T37AlC_3,
            },
            MetodoReferencia.D => (pvc, cu, dois) switch
            {
                (true, true, true) => T36CuD_2, (true, true, false) => T36CuD_3, (true, false, true) => T36AlD_2, (true, false, false) => T36AlD_3,
                (false, true, true) => T37CuD_2, (false, true, false) => T37CuD_3, (false, false, true) => T37AlD_2, _ => T37AlD_3,
            },
            MetodoReferencia.E => (pvc, cu, dois) switch
            {
                (true, true, true) => T38CuE_2, (true, true, false) => T38CuE_3, (true, false, true) => T38AlE_2, (true, false, false) => T38AlE_3,
                (false, true, true) => T39CuE_2, (false, true, false) => T39CuE_3, (false, false, true) => T39AlE_2, _ => T39AlE_3,
            },
            MetodoReferencia.F => (pvc, cu, dois) switch
            {
                (true, true, true) => T38CuF_2, (true, true, false) => T38CuF_3, (true, false, true) => T38AlF_2, (true, false, false) => T38AlF_3,
                (false, true, true) => T39CuF_2, (false, true, false) => T39CuF_3, (false, false, true) => T39AlF_2, _ => T39AlF_3,
            },
            // F no mesmo plano, G horizontal e G vertical só têm coluna de três condutores carregados.
            MetodoReferencia.FPlano => (pvc, cu) switch { (true, true) => T38CuFPlano_3, (true, false) => T38AlFPlano_3, (false, true) => T39CuFPlano_3, _ => T39AlFPlano_3 },
            MetodoReferencia.G => (pvc, cu) switch { (true, true) => T38CuG_3, (true, false) => T38AlG_3, (false, true) => T39CuG_3, _ => T39AlG_3 },
            MetodoReferencia.GVertical => (pvc, cu) switch { (true, true) => T38CuGV_3, (true, false) => T38AlGV_3, (false, true) => T39CuGV_3, _ => T39AlGV_3 },
            _ => throw new ArgumentOutOfRangeException(nameof(metodo)),
        };
    }
}
