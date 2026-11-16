using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsersService.Application.UsersService.Abstractions
{
    public interface IJtiCheckService
    {
        Task<bool> JtiCheck(string id, string jti);
    }
}
