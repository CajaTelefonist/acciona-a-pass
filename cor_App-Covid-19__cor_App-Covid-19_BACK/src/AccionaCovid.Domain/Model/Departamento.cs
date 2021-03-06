using AccionaCovid.Domain.Core;
using System;
using System.Collections.Generic;

namespace AccionaCovid.Domain.Model
{
    public partial class Departamento : Entity<Departamento>
    {
        public Departamento()
        {
            FichaLaboral = new HashSet<FichaLaboral>();
        }

        public string Nombre { get; set; }
        public long IdWorkday { get; set; }

        public virtual ICollection<FichaLaboral> FichaLaboral { get; set; }
    }
}
