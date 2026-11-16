using Microsoft.EntityFrameworkCore;
using UsersService.Domain.Entities;

namespace UsersService.Domain
{
    public class UserServiceDbContext(DbContextOptions<UserServiceDbContext> options)
        : DbContext(options)
    {
        public DbSet<UserEntity> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            base.OnModelCreating(modelBuilder);
        }
    }
}
