using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Challonge.Models
{
    public class Result<T>
    {
        public T Value { get; set; }
        public bool Succeeded { get; }
        public string Message { get; }

        public Result(T value, bool succeeded, string message = null)
        {
            Value = value;
            Succeeded = succeeded;
            Message = message;
        }

        // HAHAHAHAHAHA
        public static bool operator ==(Result<T> result, bool value)
        {
            return result?.Succeeded == value;
        }

        public static bool operator !=(Result<T> result, bool value)
        {
            return result?.Succeeded != value;
        }

        public static bool operator !(Result<T> result)
        {
            return result?.Succeeded != true;
        }
    }

    public class Success<T> : Result<T>
    {
        public Success(T value) : base(value, true)
        {
        }
    }

    public class Failure<T> : Result<T>
    {
        public Failure(string message) : base(default(T), false, message)
        {
        }

        public Failure(Exception exception) : base(default(T), false, exception.Message)
        {
        }
    }
}
