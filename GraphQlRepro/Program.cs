using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ReproDbContext>(ctx => ctx.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=GraphQlRepro;Trusted_Connection=True;MultipleActiveResultSets=true"));

var config = new MapperConfiguration(cfg =>
{
    cfg.CreateMap<ParentModel, ParentDto>().ForMember(p => p.Children, p => p.MapFrom(x => x.Children));
    cfg.CreateMap<ChildModel, ChildDto>().IncludeAllDerived();


    cfg.CreateMap<ChildModelA, ChildDtoA>();
    cfg.CreateMap<ChildModelB, ChildDtoB>();
});

var app = builder.Build();

var scope = app.Services.CreateScope();
var ctx = scope.ServiceProvider.GetRequiredService<ReproDbContext>();

ctx.Database.EnsureCreated();

var parent = ctx.Parents.FirstOrDefault();
if (parent == null)
{
    parent = new ParentModel()
    {
        Children = new List<ChildModel>()
        {
            new ChildModelA() { A = "A" },
            new ChildModelB() { B = "B" }
        }
    };

    ctx.Parents.Add(parent);
    ctx.SaveChanges();
}

{
    var projection = ctx.Parents.Include(p => p.Children);
    var query = projection.ToQueryString();
    var result = projection.ToList();
}

{
    var projection = ctx.Parents.ProjectTo<ParentDto>(config);
    var query = projection.ToQueryString();
    var result = projection.ToList();
}

public class ReproDbContext : DbContext
{
    public DbSet<ParentModel> Parents { get; set; }

    public ReproDbContext(DbContextOptions<ReproDbContext> options) : base(options)
    {
        
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ChildModelA>();
        modelBuilder.Entity<ChildModelB>();
    }
}

public class ParentModel
{
    public int Id { get; set; }
    public ICollection<ChildModel> Children { get; set; }
}

public class ChildModel
{
    public int Id { get; set; }
}

public class ChildModelA : ChildModel
{
    public string A { get; set; }
}

public class ChildModelB : ChildModel
{
    public string B { get; set; }
}

public class ParentDto
{
    public int Id { get; set; }
    public List<ChildDto> Children { get; set; } = new List<ChildDto>();
}

public class ChildDto
{
    public int Id { get; set; }
}

public class ChildDtoA : ChildDto
{
    public string A { get; set; }
}

public class ChildDtoB : ChildDto
{
    public string B { get; set; }
}
