﻿using System.ComponentModel.DataAnnotations.Schema;

namespace OpenNos.DAL.EF.MySQL
{
    [Table(nameof(SpecialistInstance))]
    public class SpecialistInstance : WearableInstance
    {
        #region Properties

        public short SlDamage { get; set; }
        public short SlDefence { get; set; }
        public short SlElement { get; set; }
        public short SlHP { get; set; }
        public byte SpDamage { get; set; }
        public byte SpDark { get; set; }
        public byte SpDefence { get; set; }
        public byte SpElement { get; set; }
        public byte SpFire { get; set; }
        public byte SpHP { get; set; }
        public byte SpLevel { get; set; }
        public byte SpLight { get; set; }
        public byte SpStoneUpgrade { get; set; }
        public byte SpWater { get; set; }
        public long SpXp { get; set; }

        #endregion
    }
}