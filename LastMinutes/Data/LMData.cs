﻿using Microsoft.EntityFrameworkCore;


namespace LastMinutes.Data
{
    public class LMData : DbContext
    {

        public LMData(DbContextOptions<LMData> options)
            : base(options)
        {

        }

        public DbSet<LastMinutes.Models.LMData.Queue> Queue { get; set; } = default!;
        public DbSet<LastMinutes.Models.LMData.Results> Results { get; set; } = default!;

    }
}
