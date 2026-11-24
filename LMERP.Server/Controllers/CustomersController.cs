using LMERP.Application.Contracts;
using LMERP.Application.Services;
using LMERP.Server.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMERP.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/customers")]
public sealed class CustomersController(ILoanManagementService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BaseResponse<IReadOnlyList<CustomerResponse>>>> GetAll(CancellationToken cancellationToken) =>
        Ok(BaseResponse<IReadOnlyList<CustomerResponse>>.Succeeded(await service.GetCustomersAsync(cancellationToken)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BaseResponse<CustomerResponse>>> GetById(int id, CancellationToken cancellationToken) =>
        ToActionResult(await service.GetCustomerAsync(id, cancellationToken));

    [Authorize(Roles = "Administrator,LoanOfficer")]
    [HttpPost]
    public async Task<ActionResult<BaseResponse<CustomerResponse>>> Create(CustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateCustomerAsync(request, cancellationToken);
        if (result.Status != OperationStatus.Success) return ToActionResult(result);
        return StatusCode(StatusCodes.Status201Created, BaseResponse<CustomerResponse>.Succeeded(result.Value!, result.Message));
    }

    [Authorize(Roles = "Administrator,LoanOfficer")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<BaseResponse<CustomerResponse>>> Update(int id, CustomerRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await service.UpdateCustomerAsync(id, request, cancellationToken));

    [Authorize(Roles = "Administrator")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<BaseResponse<bool>>> Delete(int id, CancellationToken cancellationToken) =>
        ToActionResult(await service.DeleteCustomerAsync(id, cancellationToken));

    private ActionResult<BaseResponse<T>> ToActionResult<T>(OperationResult<T> result) => result.Status switch
    {
        OperationStatus.Success => Ok(BaseResponse<T>.Succeeded(result.Value!, result.Message)),
        OperationStatus.NotFound => NotFound(BaseResponse<T>.Failed(result.Message)),
        OperationStatus.Conflict => Conflict(BaseResponse<T>.Failed(result.Message)),
        OperationStatus.BadRequest => BadRequest(BaseResponse<T>.Failed(result.Message)),
        _ => StatusCode(StatusCodes.Status500InternalServerError, BaseResponse<T>.Failed("An unexpected error occurred."))
    };
}
