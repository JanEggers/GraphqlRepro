using AutoMapper;
using AutoMapper.QueryableExtensions;
using HotChocolate;
using HotChocolate.Data;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ReproDbContext>(ctx => ctx.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=GraphQlRepro;Trusted_Connection=True;MultipleActiveResultSets=true"));

builder.Services.AddSingleton<AutoMapper.IConfigurationProvider>(c => new MapperConfiguration(cfg =>
{
    cfg.CreateMap<ParentModel, ParentDto>().ForMember(p => p.Children, p => p.MapFrom(x => x.Children));
    cfg.CreateMap<ChildModel, ChildDto>().IncludeAllDerived();


    cfg.CreateMap<ChildModelA, ChildDtoA>();
    cfg.CreateMap<ChildModelB, ChildDtoB>();
}));

builder.Services.AddControllers();

builder.Services.AddGraphQLServer()
    .RegisterDbContext<ReproDbContext>()
    .AddProjections()
    .AddFiltering()
    .AddSorting()
    .AddInterfaceType<ChildModel>()
    .AddType<ChildModelA>()
    .AddType<ChildModelB>()
    .AddInterfaceType<ChildDto>()
    .AddType<ChildDtoA>()
    .AddType<ChildDtoB>()
    .AddQueryType<Query>();

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

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseEndpoints(endpoints => endpoints.MapGraphQL());

//app.MapGraphQL();

app.Run();

public class Query
{
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<ParentModel> GetParents(ReproDbContext ctx)
    {
        return ctx.Parents;
    }

    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<ParentDto> GetParentDtos([Service] ReproDbContext ctx, [Service] AutoMapper.IConfigurationProvider autoMapperConfig)
    {
        var result = ctx.Parents.ProjectTo<ParentDto>(autoMapperConfig).ToList();
        return result.AsQueryable();
    }
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
