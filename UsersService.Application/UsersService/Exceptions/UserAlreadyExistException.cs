using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsersService.Application.UsersService.Exceptions
{
    public class UserAlreadyExistException : AppException
    {
        public UserAlreadyExistException(string message)
        : base(message){ }
        public UserAlreadyExistException(string message, Exception inner)
        : base(message, inner) { }
    }
}
