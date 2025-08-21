using BlazorCamPortal.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorCamPortal.Infrastructure.Data
{
    public class CamPortalDBContext : DbContext
    {
        public CamPortalDBContext(DbContextOptions<CamPortalDBContext> options) : base(options) { }

        public DbSet<Camera> Cameras { get; set; }

        public DbSet<VideoChunk> VideoChunks { get; set; }
    }
}
