using System;
using System.Collections.Generic;
using System.Data;
using Orchard.ContentManagement.Drivers;
using Orchard.ContentManagement.MetaData;
using Orchard.ContentManagement.MetaData.Builders;
using Orchard.Core.Contents.Extensions;
using Orchard.Data.Migration;
using EXPEDIT.Tickets.Models;

namespace EXPEDIT.Tickets {
    public class Migrations : DataMigrationImpl {

        public int Create() {
            SchemaBuilder.CreateTable(typeof(MailTicketPartRecord).Name, table => table
               .ContentPartRecord()               
               .Column("ApplicationID", DbType.Guid)
               );


            ContentDefinitionManager.AlterTypeDefinition("MailTicket",
                cfg => cfg
                    .WithPart("MailTicketPart")
                //.Creatable(true)
                    );

            return 1;
        }
       
    }
}