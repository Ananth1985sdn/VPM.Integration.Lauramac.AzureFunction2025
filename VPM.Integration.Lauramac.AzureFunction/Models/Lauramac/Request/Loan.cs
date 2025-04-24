using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Request
{
    public class Loan
    {
        [JsonProperty("Foreign National")]
        public string ForeignNational { get; set; }

        [JsonProperty("Zip")]
        public string Zip { get; set; }

        [JsonProperty("Borrower First Name")]
        public string BorrowerFirstName { get; set; }

        [JsonProperty("Address")]
        public string Address { get; set; }

        [JsonProperty("Loan Number")]
        public string LoanNumber { get; set; }

        [JsonProperty("FICO")]
        public string Fico { get; set; }

        [JsonProperty("LoanID")]
        public string LoanID { get; set; }

        [JsonProperty("Prop Type")]
        public string PropType { get; set; }

        [JsonProperty("Loan Term")]
        public string LoanTerm { get; set; }

        [JsonProperty("Appraised Value")]
        public string AppraisedValue { get; set; }

        [JsonProperty("Escrow Flag")]
        public string EscrowFlag { get; set; }

        [JsonProperty("Occupancy")]
        public string Occupancy { get; set; }

        [JsonProperty("Loan Amount")]
        public string LoanAmount { get; set; }

        [JsonProperty("Original CLTV")]
        public string OriginalCLTV { get; set; }

        [JsonProperty("Note Rate")]
        public string NoteRate { get; set; }

        [JsonProperty("Doc Type")]
        public string DocType { get; set; }

        [JsonProperty("Purchase Price")]
        public string PurchasePrice { get; set; }

        [JsonProperty("Lien Position")]
        public string LienPosition { get; set; }

        [JsonProperty("Purpose")]
        public string Purpose { get; set; }

        [JsonProperty("Borrower SSN")]
        public string BorrowerSSN { get; set; }

        [JsonProperty("City")]
        public string City { get; set; }

        [JsonProperty("Borrower Last Name")]
        public string BorrowerLastName { get; set; }

        [JsonProperty("State")]
        public string State { get; set; }

        [JsonProperty("Amortization Type")]
        public string AmortizationType { get; set; }

        [JsonProperty("Original LTV")]
        public string OriginalLTV { get; set; }
    }
}
