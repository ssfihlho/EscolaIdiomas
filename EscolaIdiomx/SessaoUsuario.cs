using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EscolaIdiomx
{
    internal class SessaoUsuario
    {
        public static int Codigo { get; set; }
        public static string Nome { get; set; }
        public static string Perfil { get; set; } // 'ADMIN', 'SECRETARIA', 'DOCENTE'

        public static void EncerrarSessao()
        {
            Codigo = 0;
            Nome = string.Empty;
            Perfil = string.Empty;
        }
    }



}
