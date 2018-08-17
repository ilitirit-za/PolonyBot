using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Challonge.Models
{
    public class Result
    {
        public bool Succeeded;
        public string Message;

    }

    public enum ResultStatus
    {
        Failure, 
        Success
    }
}
