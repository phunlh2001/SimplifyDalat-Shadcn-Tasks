﻿using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Net;
using TaskManagement.Data;
using TaskManagement.Data.DTOs;
using TaskManagement.Data.Models;

namespace TaskManagement.Endpoints
{
    public static class TaskEndpoints
    {
        public static void MapTaskEndpoints(this WebApplication app)
        {
            app.MapGet("/tasks", async ([AsParameters] GetTasksRequest @params, AppDbContext ctx) =>
            {
                var sortBy = @params.SortBy.ToLower();
                var sortOrder = @params.SortOrder.ToUpper();

                if (sortOrder != "ASC" && sortOrder != "DESC")
                {
                    return Results.BadRequest(new Response<string>
                    {
                        StatusCode = HttpStatusCode.BadRequest,
                        Message = "SortOrder only accept ASC or DESC value!"
                    });
                }

                if (string.IsNullOrEmpty(sortBy))
                {
                    return Results.BadRequest(new Response<string>
                    {
                        StatusCode = HttpStatusCode.BadRequest,
                        Message = "SortBy cannot be null or empty!"
                    });
                }

                var take = @params.Total <= 0 ? 5 : @params.Total;
                var pageSkip = @params.Page <= 0 ? 0 : @params.Page;

                var tasksQuery = ctx.Tasks
                                .Include(t => t.Tags)
                                .Skip(pageSkip * take)
                                .Take(take)
                                .AsNoTracking();

                Expression<Func<TaskModel, object>> sortSelector = sortBy switch
                {
                    "name" => task => task.Name,
                    "status" => task => task.Status,
                    "priority" => task => task.Priority,
                    _ => task => task.Id
                };

                tasksQuery = sortOrder == "ASC"
                    ? tasksQuery.OrderBy(sortSelector)
                    : tasksQuery.OrderByDescending(sortSelector);

                var tasks = await tasksQuery.ToListAsync();
                if (tasks.Count == 0)
                {
                    return Results.NotFound(new Response<string>
                    {
                        StatusCode = HttpStatusCode.NotFound,
                        Message = "Empty list!"
                    });
                }

                return Results.Ok(new Response<List<TaskResponse>>
                {
                    StatusCode = HttpStatusCode.OK,
                    Message = $"Get {take} tasks succesfully",
                    Data = tasks.Select(t => new TaskResponse
                    {
                        Id = t.Id,
                        Title = t.Title,
                        Name = t.Name,
                        Priority = t.Priority,
                        Status = t.Status,
                        Tags = t.Tags.Select(tag => new TagView
                        {
                            Id = tag.Id,
                            Name = tag.Name
                        }).ToList()
                    }).ToList()
                });
            }).WithName("GetTaskList").WithTags("Tasks").WithOpenApi();

            app.MapPost("/tasks/create", async (CreateTaskRequest request, AppDbContext context) =>
            {
                if (request == null)
                {
                    return Results.BadRequest(new Response<string>
                    {
                        StatusCode = HttpStatusCode.BadRequest,
                        Message = "Request body is required!"
                    });
                }

                foreach (var t in request.Tags)
                {
                    Tag tag = await context.Tags.FirstOrDefaultAsync(x => x.Id == t.Id);
                    if (tag == default)
                    {
                        var _newTag = new Tag();
                        if (t.Id == Guid.Empty) { _newTag.Id = Guid.NewGuid(); }
                        else { _newTag.Id = t.Id; }
                        _newTag.Name = t.Name;

                        context.Tags.Add(_newTag);
                    }
                    else
                    {
                        tag.Name = t.Name;
                        context.Tags.Update(tag);
                    }
                    await context.SaveChangesAsync();
                }

                var task = new TaskModel
                {
                    Id = Guid.NewGuid(),
                    Name = request.TaskName,
                    Title = request.Title,
                    Priority = request.Priority.ToString(),
                    Status = request.Status.ToString(),
                };
                context.Tasks.Add(task);
                await context.SaveChangesAsync();

                foreach (var tag in request.Tags)
                {
                    var _tag = new TaskTag
                    {
                        Id = Guid.NewGuid(),
                        TagId = tag.Id,
                        TaskId = task.Id
                    };

                    context.TaskTags.Add(_tag);
                    await context.SaveChangesAsync();
                }

                return Results.Ok(new Response<TaskModel>
                {
                    StatusCode = HttpStatusCode.OK,
                    Message = $"Create task succesfully",
                    Data = task
                });
            }).WithName("CreateTask").WithTags("Tasks").WithOpenApi();

            app.MapPut("/tasks/{id}", async (Guid id, UpdateTaskRequest request, AppDbContext context) =>
            {
                if (request == null)
                {
                    return Results.BadRequest(new Response<string>
                    {
                        StatusCode = HttpStatusCode.BadRequest,
                        Message = "Request body is required!"
                    });
                }

                var taskExisted = await context.Tasks.FirstOrDefaultAsync(t => t.Id == id);
                if (taskExisted == null)
                {
                    return Results.NotFound(new Response<string>
                    {
                        StatusCode = HttpStatusCode.NotFound,
                        Message = $"Not found any task has id: {id}"
                    });
                }
                try
                {
                    taskExisted.Title = request.Title;
                    taskExisted.Name = request.TaskName;
                    taskExisted.Status = request.Status.ToString();
                    taskExisted.Priority = request.Priority.ToString();
                    context.Tasks.Update(taskExisted);
                    await context.SaveChangesAsync();

                    return Results.Ok(new Response<string>
                    {
                        StatusCode = HttpStatusCode.OK,
                        Message = "Update task succesfully"
                    });
                }
                catch
                {
                    return Results.BadRequest(new Response<string>
                    {
                        StatusCode = HttpStatusCode.BadRequest,
                        Message = "Failed to update"
                    });
                }
            }).WithName("UpdateTask").WithTags("Tasks").WithOpenApi();

            app.MapDelete("/tasks/{id}", async (Guid id, AppDbContext context) =>
            {
                try
                {
                    var task = await context.Tasks.FirstOrDefaultAsync(x => x.Id == id);
                    if (task == null)
                    {
                        return Results.NotFound(new Response<string>
                        {
                            StatusCode = HttpStatusCode.NotFound,
                            Message = "Not found any task"
                        });
                    }

                    context.Tasks.Remove(task);
                    await context.SaveChangesAsync();

                    return Results.Ok(new Response<string>
                    {
                        StatusCode = HttpStatusCode.OK,
                        Message = "Delete tasks succesfully"
                    });
                }
                catch (Exception e)
                {
                    return Results.BadRequest(new Response<string>
                    {
                        StatusCode = HttpStatusCode.BadRequest,
                        Message = e.Message,
                    });
                }
            }).WithName("DeleteTask").WithTags("Tasks").WithOpenApi();
        }
    }
}
