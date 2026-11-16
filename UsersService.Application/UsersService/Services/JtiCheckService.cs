using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsersService.Application.UsersService.Abstractions;
using UsersService.Domain;

namespace UsersService.Application.UsersService.Services
{
    public class JtiCheckService : IJtiCheckService
    {
        private readonly IRedisService _redis;
        private readonly UserServiceDbContext _context;
        private readonly ILogger<JtiCheckService> _logger;

        public JtiCheckService(
            IRedisService redis,
            ILogger<JtiCheckService> logger,
            UserServiceDbContext context
            )
        {
            _redis = redis;
            _logger = logger;
            _context = context;
        }

        public async Task<bool> JtiCheck(string id, string jti)
        {
            var userJti = await _redis.GetDataAsync<string>(id);
            if (userJti is not null)
            {
                _logger.LogInformation("UserJti found in Redis for user {id}", id);
            }
            else
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Jti != null && x.Jti.ToString() == jti);

                if (user is not null)
                {
                    userJti = user.Jti.ToString();
                    _logger.LogInformation("UserJti found in Db for user {id}", id);
                }
                else
                {
                    _logger.LogWarning("Jti {Jti} not found in Redis or DB", jti);
                    return false;
                }
            }
            if (userJti != jti)
            {
                _logger.LogWarning(
                    "Jti mismatch, access denied");
                return false;
            }
            _logger.LogInformation("Correct Jti");

            return true;
        }
    }
}
