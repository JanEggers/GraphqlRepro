using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using AutoMapper.Internal;
using AutoMapper.QueryableExtensions.Impl;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ReproDbContext>(ctx => ctx.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=GraphQlRepro;Trusted_Connection=True;MultipleActiveResultSets=true"));

var config = new MapperConfiguration(cfg =>
{
    cfg.Internal().ProjectionMappers.Insert(0, new CollectionInheritanceMapper<ChildModel, ChildDto>());
    cfg.CreateProjection<ParentModel, ParentDto>().ForMember(p => p.Children, p => p.MapFrom(x => x.Children));
    cfg.CreateProjection<ChildModel, ChildDto>();


    cfg.CreateProjection<ChildModelA, ChildDtoA>();
    cfg.CreateProjection<ChildModelB, ChildDtoB>();
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

//{
//    var typeA = typeof(ChildModelA);
//    var typeB = typeof(ChildModelB);
//    var projection = ctx.Parents.Include(p => p.Children).Select(p => new ParentDto()
//    {
//        Id = p.Id,
//        Children = p.Children.Select(c => c.GetType() == typeA ? new ChildDtoA() { Id = ((ChildModelA)c).Id, A = ((ChildModelA)c).A } :
//            c.GetType() == typeB ? new ChildDtoB() { Id = ((ChildModelB)c).Id, B = ((ChildModelB)c).B } :
//            new ChildDto() { Id = c.Id }).ToList()
//    });
//    var query = projection.ToQueryString();
//    var result = projection.ToList();
//}

{
    var projection = ctx.Parents.ProjectTo<ParentDto>(config);
    var query = projection.ToQueryString();
    var result = projection.ToList();
}

public class CollectionInheritanceMapper<TBaseSource,TBaseDest> : IProjectionMapper
{
    public bool IsMatch(TypePair context)
    {
        return context.IsCollection()
             && context.SourceType.GetGenericArguments()[0] == typeof(TBaseSource) 
             && context.DestinationType.GetGenericArguments()[0] == typeof(TBaseDest);
    }

    public Expression Project(IGlobalConfiguration configuration, in ProjectionRequest request, Expression resolvedSource, LetPropertyMaps letPropertyMaps)
    {
        var destinationListType = request.DestinationType.GetGenericArguments()[0];
        var sourceListType = request.SourceType.GetGenericArguments()[0];
        var itemRequest = request.InnerRequest(sourceListType, destinationListType);
        var instanceParameter = Expression.Parameter(request.SourceType, "dto" + request.SourceType.Name);
        var typeMap = configuration.ResolveTypeMap(itemRequest.SourceType, itemRequest.DestinationType);

        var constructorExpression = Expression.New(itemRequest.DestinationType);
        var transformedExpressions = (Expression)Expression.MemberInit(constructorExpression, ProjectProperties(typeMap));


        foreach (var derivedType in configuration.GetIncludedTypeMaps(typeMap.IncludedDerivedTypes))
        {
            var derivedContructorExpression = Expression.New(derivedType.DestinationType);
            var derivedExpression = Expression.MemberInit(derivedContructorExpression, ProjectProperties(derivedType));
            var condition = Expression.TypeIs(instanceParameter, derivedType.SourceType);

            transformedExpressions = Expression.Condition(condition, Expression.Convert(derivedExpression, typeMap.DestinationType), transformedExpressions);
        }

        return Select(resolvedSource, transformedExpressions);

        List<MemberBinding> ProjectProperties(TypeMap localTypeMap)
        {
            var propertiesProjections = new List<MemberBinding>();
            foreach (var propertyMap in localTypeMap.PropertyMaps.Where(pm =>
                pm.CanResolveValue && pm.DestinationMember.CanBeSet() && !typeMap.ConstructorParameterMatches(pm.DestinationName))
                .OrderBy(pm => pm.DestinationMember.MetadataToken))
            {
                var propertyProjection = TryProjectMember(propertyMap);
                if (propertyProjection != null)
                {
                    propertiesProjections.Add(Expression.Bind(propertyMap.DestinationMember, propertyProjection));
                }
            }

            return propertiesProjections;
        }


    }
  
    private static readonly MethodInfo SelectMethod = typeof(Enumerable).StaticGenericMethod("Select", parametersCount: 2);
    private static Expression Select(Expression source, LambdaExpression lambda) =>
        Expression.Call(SelectMethod.MakeGenericMethod(lambda.Parameters[0].Type, lambda.ReturnType), source, lambda);
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
