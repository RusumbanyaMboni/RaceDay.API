using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Controllers;
using RaceDay.API.Data;
using RaceDay.API.Models;
using System.Security.Claims;

namespace RaceDay.API.Tests;

public class UnitTest1
{
    private RaceDayDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RaceDayDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new RaceDayDbContext(options);
    }


    // =========================================================
    // EVENT TESTS
    // =========================================================

    [Fact]
    public async Task GetEvents_ReturnsOk_WhenEventsExist()
    {
        using var context = CreateContext();

        context.Events.Add(new Event
        {
            EventId = 1,
            OrganiserId = 1,
            EventName = "Test Race",
            Description = "Unit test event",
            EventDate = DateTime.UtcNow.AddDays(10),
            Location = "Cape Town",
            Distance = 10,
            EventType = "run",
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var controller = new EventController(context);

        var result = await controller.GetEvents();

        var okResult = Assert.IsType<OkObjectResult>(result);

        Assert.NotNull(okResult.Value);
    }


    [Fact]
    public async Task GetEvent_ReturnsNotFound_WhenEventDoesNotExist()
    {
        using var context = CreateContext();

        var controller = new EventController(context);

        var result = await controller.GetEvent(999);

        Assert.IsType<NotFoundObjectResult>(result);
    }


    [Fact]
    public async Task CreateEvent_ReturnsCreated_WhenEventTypeIsProvided()
    {
        using var context = CreateContext();

        var controller = new EventController(context);

        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                "1"
            ),
            new Claim(
                ClaimTypes.Role,
                "Organiser"
            )
        };

        var identity = new ClaimsIdentity(
            claims,
            "TestAuthentication"
        );

        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext =
            new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal
                }
            };

        var eventItem = new Event
        {
            EventName = "Test Running Event",
            Description = "Testing event creation",
            EventDate = DateTime.UtcNow.AddDays(10),
            Location = "Cape Town",
            Distance = 10,
            EventType = "run"
        };

        var result = await controller.CreateEvent(eventItem);

        var createdResult =
            Assert.IsType<CreatedAtActionResult>(result);

        Assert.NotNull(createdResult.Value);

        var savedEvent = await context.Events
            .FirstOrDefaultAsync();

        Assert.NotNull(savedEvent);
        Assert.Equal("Test Running Event", savedEvent.EventName);
        Assert.Equal("run", savedEvent.EventType);
        Assert.Equal(1, savedEvent.OrganiserId);
    }


    [Fact]
    public async Task CreateEvent_UsesAuthenticatedUserAsOrganiser()
    {
        using var context = CreateContext();

        var controller = new EventController(context);

        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                "1"
            )
        };

        var identity = new ClaimsIdentity(
            claims,
            "TestAuthentication"
        );

        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext =
            new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal
                }
            };

        var eventItem = new Event
        {
            EventName = "Authenticated Organiser Test",
            Description = "Testing organiser ownership",
            EventDate = DateTime.UtcNow.AddDays(10),
            Location = "Cape Town",
            Distance = 10,
            EventType = "run"
        };

        var result = await controller.CreateEvent(eventItem);

        Assert.IsType<CreatedAtActionResult>(result);

        var savedEvent = await context.Events
            .FirstOrDefaultAsync();

        Assert.NotNull(savedEvent);
        Assert.Equal(1, savedEvent.OrganiserId);
    }


    // =========================================================
    // EVENT TYPE VALIDATION TEST
    // =========================================================

    [Fact]
    public async Task CreateEvent_ReturnsBadRequest_WhenEventTypeIsInvalid()
    {
        using var context = CreateContext();

        var controller = new EventController(context);

        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                "1"
            ),
            new Claim(
                ClaimTypes.Role,
                "Organiser"
            )
        };

        var identity = new ClaimsIdentity(
            claims,
            "TestAuthentication"
        );

        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext =
            new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal
                }
            };

        var eventItem = new Event
        {
            EventName = "Invalid Event Type Test",
            Description = "Testing invalid event type validation",
            EventDate = DateTime.UtcNow.AddDays(10),
            Location = "Cape Town",
            Distance = 10,
            EventType = "swim"
        };

        var result = await controller.CreateEvent(eventItem);

        var badRequestResult =
            Assert.IsType<BadRequestObjectResult>(result);

        Assert.NotNull(badRequestResult.Value);

        var savedEvent = await context.Events
            .FirstOrDefaultAsync();

        Assert.Null(savedEvent);
    }


    [Fact]
    public async Task UpdateEvent_ReturnsForbid_WhenUserDoesNotOwnEvent()
    {
        using var context = CreateContext();

        context.Events.Add(new Event
        {
            EventId = 1,
            OrganiserId = 1,
            EventName = "Original Event",
            Description = "Original description",
            EventDate = DateTime.UtcNow.AddDays(10),
            Location = "Cape Town",
            Distance = 10,
            EventType = "run",
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var controller = new EventController(context);

        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                "2"
            ),
            new Claim(
                ClaimTypes.Role,
                "1"
            )
        };

        var identity = new ClaimsIdentity(
            claims,
            "TestAuthentication"
        );

        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext =
            new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal
                }
            };

        var updatedEvent = new Event
        {
            EventId = 1,
            OrganiserId = 1,
            EventName = "Attempted Update",
            Description = "User should not be allowed",
            EventDate = DateTime.UtcNow.AddDays(20),
            Location = "Cape Town",
            Distance = 20,
            EventType = "run"
        };

        var result =
            await controller.UpdateEvent(1, updatedEvent);

        Assert.IsType<ForbidResult>(result);
    }


    // =========================================================
    // CATEGORY TESTS
    // =========================================================

    [Fact]
    public async Task GetCategory_ReturnsOk_WhenCategoryExists()
    {
        using var context = CreateContext();

        context.Categories.Add(new Category
        {
            CategoryId = 1,
            EventId = 1,
            CategoryName = "Under 20",
            MinimumAge = null,
            MaximumAge = 19,
            Distance = 10
        });

        await context.SaveChangesAsync();

        var controller = new CategoryController(context);

        var result = await controller.GetCategory(1);

        Assert.IsType<OkObjectResult>(result);
    }


    [Fact]
    public async Task GetCategory_ReturnsNotFound_WhenCategoryDoesNotExist()
    {
        using var context = CreateContext();

        var controller = new CategoryController(context);

        var result = await controller.GetCategory(999);

        Assert.IsType<NotFoundObjectResult>(result);
    }


    [Fact]
    public async Task GetEventCategories_ReturnsNotFound_WhenEventDoesNotExist()
    {
        using var context = CreateContext();

        var controller = new CategoryController(context);

        var result = await controller.GetEventCategories(999);

        Assert.IsType<NotFoundObjectResult>(result);
    }


    // =========================================================
    // CATEGORY OWNERSHIP TEST
    // =========================================================

    [Fact]
    public async Task CreateCategory_ReturnsForbid_WhenOrganizerDoesNotOwnEvent()
    {
        // Arrange
        using var context = CreateContext();

        context.Events.Add(new Event
        {
            EventId = 1,
            OrganiserId = 1,
            EventName = "Ownership Test Race",
            Description = "Testing category ownership",
            EventDate = DateTime.UtcNow.AddDays(10),
            Location = "Cape Town",
            Distance = 10,
            EventType = "run",
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var controller = new CategoryController(context);

        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                "2"
            ),
            new Claim(
                ClaimTypes.Role,
                "1"
            )
        };

        var identity = new ClaimsIdentity(
            claims,
            "TestAuthentication"
        );

        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext =
            new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal
                }
            };

        var category = new Category
        {
            CategoryName = "10km Senior",
            MinimumAge = 18,
            MaximumAge = 39,
            Distance = 10
        };

        // Act
        var result =
            await controller.CreateCategory(1, category);

        // Assert
        Assert.IsType<ForbidResult>(result);

        var savedCategory = await context.Categories
            .FirstOrDefaultAsync();

        Assert.Null(savedCategory);
    }


    // =========================================================
    // RESULT TESTS
    // =========================================================

    [Fact]
    public async Task GetResult_ReturnsNotFound_WhenResultDoesNotExist()
    {
        using var context = CreateContext();

        var controller = new ResultController(context);

        var result = await controller.GetResult(999);

        Assert.IsType<NotFoundObjectResult>(result);
    }


    [Fact]
    public async Task GetEventResults_ReturnsNotFound_WhenEventDoesNotExist()
    {
        using var context = CreateContext();

        var controller = new ResultController(context);

        var result = await controller.GetEventResults(999);

        Assert.IsType<NotFoundObjectResult>(result);
    }


    // =========================================================
    // AUTHENTICATION TESTS
    // =========================================================

    [Fact]
    public async Task Register_ReturnsCreated_WhenValidUserIsProvided()
    {
        using var context = CreateContext();

        context.Roles.Add(new Role
        {
            RoleId = 2,
            RoleName = "Participant"
        });

        await context.SaveChangesAsync();

        var configuration =
            new Microsoft.Extensions.Configuration.ConfigurationManager();

        var controller = new AuthController(
            context,
            configuration);

        var request = new AuthController.RegisterRequest
        {
            FirstName = "Test",
            LastName = "Participant",
            Email = "test@example.com",
            Password = "Password123",
            PhoneNumber = "0821234567",
            RoleId = 2
        };

        var result = await controller.Register(request);

        var createdResult =
            Assert.IsType<CreatedAtActionResult>(result);

        Assert.NotNull(createdResult.Value);

        var savedUser = await context.Users
            .FirstOrDefaultAsync(u =>
                u.Email == "test@example.com");

        Assert.NotNull(savedUser);

        Assert.Equal("Test", savedUser.FirstName);
        Assert.Equal("Participant", savedUser.LastName);
        Assert.Equal(2, savedUser.RoleId);

        Assert.NotEqual(
            "Password123",
            savedUser.PasswordHash);

        Assert.True(
            BCrypt.Net.BCrypt.Verify(
                "Password123",
                savedUser.PasswordHash));
    }


    [Fact]
    public async Task Register_ReturnsConflict_WhenEmailAlreadyExists()
    {
        using var context = CreateContext();

        context.Roles.Add(new Role
        {
            RoleId = 2,
            RoleName = "Participant"
        });

        context.Users.Add(new User
        {
            UserId = 1,
            FirstName = "Existing",
            LastName = "User",
            Email = "existing@example.com",
            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword("Password123"),
            RoleId = 2,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var configuration =
            new Microsoft.Extensions.Configuration.ConfigurationManager();

        var controller = new AuthController(
            context,
            configuration);

        var request = new AuthController.RegisterRequest
        {
            FirstName = "Another",
            LastName = "User",
            Email = "existing@example.com",
            Password = "Password123",
            RoleId = 2
        };

        var result = await controller.Register(request);

        Assert.IsType<ConflictObjectResult>(result);
    }


    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordIsIncorrect()
    {
        using var context = CreateContext();

        context.Roles.Add(new Role
        {
            RoleId = 2,
            RoleName = "Participant"
        });

        context.Users.Add(new User
        {
            UserId = 1,
            FirstName = "Test",
            LastName = "Participant",
            Email = "test@example.com",
            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword("CorrectPassword123"),
            RoleId = 2,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var configuration =
            new Microsoft.Extensions.Configuration.ConfigurationManager();

        var controller = new AuthController(
            context,
            configuration);

        var request = new AuthController.LoginRequest
        {
            Email = "test@example.com",
            Password = "WrongPassword123"
        };

        var result = await controller.Login(request);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }


    // =========================================================
    // ENROLMENT TESTS
    // =========================================================

    [Fact]
    public async Task CreateEnrolment_ReturnsCreated_WhenValidParticipantEnrols()
    {
        // Arrange
        using var context = CreateContext();

        context.Events.Add(new Event
        {
            EventId = 1,
            OrganiserId = 1,
            EventName = "Participant Enrolment Test",
            Description = "Testing participant enrolment",
            EventDate = DateTime.UtcNow.AddDays(10),
            Location = "Cape Town",
            Distance = 10,
            EventType = "run",
            CreatedAt = DateTime.UtcNow
        });

        context.Categories.Add(new Category
        {
            CategoryId = 1,
            EventId = 1,
            CategoryName = "10km",
            MinimumAge = 18,
            MaximumAge = 60,
            Distance = 10
        });

        await context.SaveChangesAsync();

        var controller = new EnrolmentController(context);

        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                "2"
            ),
            new Claim(
                ClaimTypes.Role,
                "Participant"
            )
        };

        var identity = new ClaimsIdentity(
            claims,
            "TestAuthentication"
        );

        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext =
            new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal
                }
            };

        var request =
            new EnrolmentController.CreateEnrolmentRequest
            {
                CategoryId = 1
            };

        // Act
        var result =
            await controller.CreateEnrolment(1, request);

        // Assert
        var createdResult =
            Assert.IsType<CreatedAtActionResult>(result);

        Assert.NotNull(createdResult.Value);

        var savedEnrolment =
            await context.Enrolments.FirstOrDefaultAsync();

        Assert.NotNull(savedEnrolment);

        Assert.Equal(2, savedEnrolment.ParticipantId);
        Assert.Equal(1, savedEnrolment.EventId);
        Assert.Equal(1, savedEnrolment.CategoryId);
        Assert.Equal("Registered", savedEnrolment.Status);
    }


    [Fact]
    public async Task CreateEnrolment_ReturnsConflict_WhenParticipantAlreadyEnrolled()
    {
        // Arrange
        using var context = CreateContext();

        context.Events.Add(new Event
        {
            EventId = 1,
            OrganiserId = 1,
            EventName = "Duplicate Enrolment Test",
            Description = "Testing duplicate enrolment prevention",
            EventDate = DateTime.UtcNow.AddDays(10),
            Location = "Cape Town",
            Distance = 10,
            EventType = "run",
            CreatedAt = DateTime.UtcNow
        });

        context.Categories.Add(new Category
        {
            CategoryId = 1,
            EventId = 1,
            CategoryName = "10km",
            MinimumAge = 18,
            MaximumAge = 60,
            Distance = 10
        });

        context.Enrolments.Add(new Enrolment
        {
            EnrolmentId = 1,
            ParticipantId = 2,
            EventId = 1,
            CategoryId = 1,
            EnrolmentDate = DateTime.UtcNow.AddDays(-1),
            Status = "Registered"
        });

        await context.SaveChangesAsync();

        var controller = new EnrolmentController(context);

        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                "2"
            ),
            new Claim(
                ClaimTypes.Role,
                "Participant"
            )
        };

        var identity = new ClaimsIdentity(
            claims,
            "TestAuthentication"
        );

        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext =
            new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal
                }
            };

        var request =
            new EnrolmentController.CreateEnrolmentRequest
            {
                CategoryId = 1
            };

        // Act
        var result =
            await controller.CreateEnrolment(1, request);

        // Assert
        Assert.IsType<ConflictObjectResult>(result);

        var enrolmentCount =
            await context.Enrolments.CountAsync();

        Assert.Equal(1, enrolmentCount);
    }
}