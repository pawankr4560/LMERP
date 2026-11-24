using LMERP.Application.Contracts;
using LMERP.Application.Services;
using LMERP.Server.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMERP.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/loans")]
public sealed class LoansController(ILoanManagementService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BaseResponse<IReadOnlyList<LoanResponse>>>> GetAll(CancellationToken cancellationToken) =>
        Ok(BaseResponse<IReadOnlyList<LoanResponse>>.Succeeded(await service.GetLoansAsync(cancellationToken)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BaseResponse<LoanResponse>>> GetById(int id, CancellationToken cancellationToken) =>
        ToActionResult(await service.GetLoanAsync(id, cancellationToken));

    [Authorize(Roles = "Administrator,LoanOfficer")]
    [HttpPost]
    public async Task<ActionResult<BaseResponse<LoanResponse>>> Create(LoanRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateLoanAsync(request, cancellationToken);
        if (result.Status != OperationStatus.Success) return ToActionResult(result);
        return StatusCode(StatusCodes.Status201Created, BaseResponse<LoanResponse>.Succeeded(result.Value!, result.Message));
    }

    private ActionResult<BaseResponse<T>> ToActionResult<T>(OperationResult<T> result) => result.Status switch
    {
        OperationStatus.Success => Ok(BaseResponse<T>.Succeeded(result.Value!, result.Message)),
        OperationStatus.NotFound => NotFound(BaseResponse<T>.Failed(result.Message)),
        OperationStatus.Conflict => Conflict(BaseResponse<T>.Failed(result.Message)),
        OperationStatus.BadRequest => BadRequest(BaseResponse<T>.Failed(result.Message)),
        _ => StatusCode(StatusCodes.Status500InternalServerError, BaseResponse<T>.Failed("An unexpected error occurred."))
    };
}
