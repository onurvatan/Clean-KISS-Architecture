using API.Extensions;
using API.Models;
using Application.Abstractions;
using Application.DTOs;
using Application.Handlers.DeleteStudent;
using Application.Handlers.GetStudent;
using Application.Handlers.RegisterStudent;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
public class StudentsController : ControllerBase
{
    private readonly IHandler<RegisterStudentCommand, StudentDto> _registerHandler;
    private readonly IHandler<GetStudentQuery, StudentDto> _getHandler;
    private readonly IHandler<DeleteStudentCommand> _deleteHandler;

    public StudentsController(
        IHandler<RegisterStudentCommand, StudentDto> registerHandler,
        IHandler<GetStudentQuery, StudentDto> getHandler,
        IHandler<DeleteStudentCommand> deleteHandler)
    {
        _registerHandler = registerHandler;
        _getHandler = getHandler;
        _deleteHandler = deleteHandler;
    }

    [HttpGet("{id:guid}", Name = nameof(GetById))]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetStudentQuery(id);
        var result = await _getHandler.Handle(query, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterStudentRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterStudentCommand(request.Name, request.Email);
        var result = await _registerHandler.Handle(command, cancellationToken);
        return result.ToActionResult(this, nameof(GetById), dto => new { id = dto.Id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeleteStudentCommand(id);
        var result = await _deleteHandler.Handle(command, cancellationToken);
        return result.ToActionResult(this);
    }
}
