//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace OpenNos.DAL.EF.MySQL.DB
{
    using System;
    using System.Collections.Generic;
    
    public partial class GeneralLog
    {
        public long LogId { get; set; }
        public long AccountId { get; set; }
        public string IpAddress { get; set; }
        public System.DateTime Timestamp { get; set; }
        public string LogType { get; set; }
        public string LogData { get; set; }
        public Nullable<long> CharacterId { get; set; }
    
        public virtual Account account { get; set; }
        public virtual Character character { get; set; }
    }
}
