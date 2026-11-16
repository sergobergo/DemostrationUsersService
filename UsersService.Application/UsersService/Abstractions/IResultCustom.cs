using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsersService.Application.UsersService.Abstractions
{
    public interface IResultCustom
    {
         bool IsFailure {  get; }
         bool IsSuccess { get; }
         object? Data { get; }
         string? Error { get; }
    }
}
