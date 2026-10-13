namespace FitnessMerchant.Api.Common;

/// <summary>Extra endpoints mapped at startup when registered in DI. Used only by integration tests.</summary>
public interface IEndpointMapper
{
    void Map(IEndpointRouteBuilder app);
}
