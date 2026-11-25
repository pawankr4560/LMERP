using LMERP.Application.Contracts;
using LMERP.Application.Services;
using LMERP.Server.Responses;
using LMERP.Server.Auth;
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

    [Authorize(Roles = Roles.LoanStaff)]
    [HttpGet("{id:int}/emi-schedule")]
    public async Task<ActionResult<BaseResponse<IReadOnlyList<EmiScheduleResponse>>>> GetEmiSchedule(int id, CancellationToken cancellationToken) =>
        ToActionResult(await service.GetEmiScheduleAsync(id, cancellationToken));

    [Authorize(Roles = Roles.LoanStaff)]
    [HttpPost]
    public async Task<ActionResult<BaseResponse<LoanResponse>>> Create(LoanRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateLoanAsync(request, cancellationToken);
        if (result.Status != OperationStatus.Success) return ToActionResult(result);
        return StatusCode(StatusCodes.Status201Created, BaseResponse<LoanResponse>.Succeeded(result.Value!, result.Message));
    }

    [Authorize(Roles = Roles.LoanStaff)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<BaseResponse<LoanResponse>>> Update(int id, LoanUpdateRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await service.UpdateLoanAsync(id, request, cancellationToken));

    [Authorize(Roles = Roles.LoanStaff)]
    [HttpPost("{id:int}/disburse")]
    public async Task<ActionResult<BaseResponse<LoanResponse>>> Disburse(int id, CancellationToken cancellationToken) =>
        ToActionResult(await service.DisburseLoanAsync(id, cancellationToken));

    [Authorize(Roles = Roles.LoanStaff)]
    [HttpPost("{id:int}/reject")]
    public async Task<ActionResult<BaseResponse<LoanResponse>>> Reject(int id, LoanRejectionRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await service.RejectLoanAsync(id, request.Reason, cancellationToken));

    [Authorize(Roles = Roles.LoanStaff)]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<BaseResponse<bool>>> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await service.DeleteLoanAsync(id, cancellationToken);
        if (result.Status != OperationStatus.Success) return ToActionResult(result);
        return Ok(BaseResponse<bool>.Succeeded(result.Value, result.Message));
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
