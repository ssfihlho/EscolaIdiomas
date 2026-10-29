using System;
using System.Data;
using System.Globalization;
using System.Windows.Forms;

namespace EscolaIdiomx
{
    /// <summary>
    /// Código compartilhado pelas telas AvaliacoesCRUD e LancamentoAvaliacoes.
    /// Centralizar aqui garante que as duas telas carreguem os combos e
    /// validem a nota exatamente do mesmo jeito.
    /// </summary>
    internal static class AvaliacaoHelper
    {
        /// <summary>
        /// Converte o texto digitado em nota. Aceita vírgula ou ponto ("8,5" ou "8.5"),
        /// entre 0 e 10, com no máximo 2 casas decimais (a coluna é DECIMAL(4,2)).
        /// </summary>
        public static bool TentarLerNota(string texto, out decimal nota)
        {
            nota = 0;

            if (string.IsNullOrWhiteSpace(texto))
                return false;

            // Normaliza o separador para que "8,5" não seja lido como 85 em outras culturas.
            string normalizado = texto.Trim().Replace(',', '.');

            if (!decimal.TryParse(normalizado, NumberStyles.AllowDecimalPoint,
                                  CultureInfo.InvariantCulture, out nota))
                return false;

            return nota >= 0 && nota <= 10 && decimal.Round(nota, 2) == nota;
        }

        /// <summary>Carrega o combo de alunos (exibe Nome, valor = Codigo), sem seleção.</summary>
        public static void CarregarComboAlunos(ComboBox combo)
        {
            DataTable alunos = new DAO().ListarAlunos();

            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.DataSource = alunos;
            combo.DisplayMember = "Nome";
            combo.ValueMember = "Codigo";
            combo.SelectedIndex = -1;
        }

        /// <summary>
        /// Carrega o combo de turmas no formato "Turma #3 - Inglês Básico (08:00 - 10:00)".
        /// Valor = Codigo da turma; a linha também traz CursoCodigo.
        /// </summary>
        public static void CarregarComboTurmas(ComboBox combo)
        {
            DataTable origem = new DAO().ListarTurmas();

            combo.DropDownStyle = ComboBoxStyle.DropDownList;

            // Consulta falhou (já foi exibida a mensagem de erro): combo vazio.
            if (origem.Columns.Count == 0)
            {
                combo.DataSource = null;
                return;
            }

            // ListarTurmas devolve uma linha por professor (LEFT JOIN em lecionar);
            // aqui removemos as repetições para cada turma aparecer uma única vez.
            DataTable turmas = new DataView(origem).ToTable(
                true, "Codigo", "CursoCodigo", "Curso", "HorarioInicio", "HorarioFim");

            turmas.Columns.Add("Descricao", typeof(string));

            foreach (DataRow linha in turmas.Rows)
            {
                string horario = string.Empty;

                if (linha["HorarioInicio"] is TimeSpan && linha["HorarioFim"] is TimeSpan)
                {
                    horario = " (" + ((TimeSpan)linha["HorarioInicio"]).ToString(@"hh\:mm") + " - "
                                   + ((TimeSpan)linha["HorarioFim"]).ToString(@"hh\:mm") + ")";
                }

                linha["Descricao"] = "Turma #" + linha["Codigo"] + " - " + linha["Curso"] + horario;
            }

            combo.DataSource = turmas;
            combo.DisplayMember = "Descricao";
            combo.ValueMember = "Codigo";
            combo.SelectedIndex = -1;
        }
    }
}
