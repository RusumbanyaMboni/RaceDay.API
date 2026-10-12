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
        // Arrange
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

        // Act
        var result = await controller.GetEvents();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }


    [Fact]
    public async Task GetEvent_ReturnsNotFound_WhenEventDoesNotExist()
    {
        // Arrange
        using var context = CreateContext();

        var controller = new EventController(context);

        // Act
        var result = await controller.GetEvent(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }


    [Fact]
    public async Task CreateEvent_ReturnsCreated_WhenEventTypeIsProvided()
    {
        // Arrange
        using var context = CreateContext();

        var controller = new EventController(context);

        // Create an authenticated organiser
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
            EventName = "Test Marathon Event",
            Description = "Testing event creation",
            EventDate = DateTime.UtcNow.AddDays(10),
            Location = "Cape Town",
            Distance = 10,
            EventType = "marathon"
        };

        // Act
        var result = await controller.CreateEvent(eventItem);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);

        Assert.NotNull(createdResult.Value);

        // Confirm the event was actually saved
        var savedEvent = await context.Events
            .FirstOrDefaultAsync();

        Assert.NotNull(savedEvent);
        Assert.Equal("Test Marathon Event", savedEvent.EventName);
        Assert.Equal("marathon", savedEvent.EventType);
        Assert.Equal(1, savedEvent.OrganiserId);
    }


    [Fact]
    public async Task CreateEvent_UsesAuthenticatedUserAsOrganiser()
    {
        // Arrange
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

        // Act
        var result = await controller.CreateEvent(eventItem);

        // Assert
        Assert.IsType<CreatedAtActionResult>(result);

        var savedEvent = await context.Events
            .FirstOrDefaultAsync();

        Assert.NotNull(savedEvent);
        Assert.Equal(1, savedEvent.OrganiserId);
    }


    [Fact]
    public async Task UpdateEvent_ReturnsForbid_WhenOrganiserDoesNotOwnEvent()
    {
        // Arrange
        using var context = CreateContext();

        // Event belongs to Organiser 2
        context.Events.Add(new Event
        {
            EventId = 1,
            OrganiserId = 2,
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

        // Authenticate as Organiser 1
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

        // Try to update Organiser 2's event
        var eventItem = new Event
        {
            EventId = 1,
            EventName = "Hacked Event",
            Description = "This should not be allowed",
            EventDate = DateTime.UtcNow.AddDays(20),
            Location = "Johannesburg",
            Distance = 50,
            EventType = "run"
        };

        // Act
        var result = await controller.UpdateEvent(1, eventItem);

        // Assert
        Assert.IsType<ForbidResult>(result);

        // Confirm the original event was not changed
        var savedEvent = await context.Events
            .FirstOrDefaultAsync(e => e.EventId == 1);

        Assert.NotNull(savedEvent);
        Assert.Equal("Original Event", savedEvent.EventName);
        Assert.Equal(2, savedEvent.OrganiserId);
    }


    // =========================================================
    // CATEGORY TESTS
    // =========================================================

    [Fact]
    public async Task GetCategory_ReturnsOk_WhenCategoryExists()
    {
        // Arrange
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

        // Act
        var result = await controller.GetCategory(1);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }


    [Fact]
    public async Task GetCategory_ReturnsNotFound_WhenCategoryDoesNotExist()
    {
        // Arrange
        using var context = CreateContext();

        var controller = new CategoryController(context);

        // Act
        var result = await controller.GetCategory(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }


    [Fact]
    public async Task GetEventCategories_ReturnsNotFound_WhenEventDoesNotExist()
    {
        // Arrange
        using var context = CreateContext();

        var controller = new CategoryController(context);

        // Act
        var result = await controller.GetEventCategories(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }


    // =========================================================
    // RESULT TESTS
    // =========================================================

    [Fact]
    public async Task GetResult_ReturnsNotFound_WhenResultDoesNotExist()
    {
        // Arrange
        using var context = CreateContext();

        var controller = new ResultController(context);

        // Act
        var result = await controller.GetResult(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }


    [Fact]
    public async Task GetEventResults_ReturnsNotFound_WhenEventDoesNotExist()
    {
        // Arrange
        using var context = CreateContext();

        var controller = new ResultController(context);

        // Act
        var result = await controller.GetEventResults(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }


    // =========================================================
    // AUTHENTICATION TESTS
    // =========================================================

    [Fact]
    public async Task Register_ReturnsCreated_WhenValidUserIsProvided()
    {
        // Arrange
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

        // Act
        var result = await controller.Register(request);

        // Assert
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
        // Arrange
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

        // Act
        var result = await controller.Register(request);

        // Assert
        Assert.IsType<ConflictObjectResult>(result);
    }


    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordIsIncorrect()
    {
        // Arrange
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

        // Act
        var result = await controller.Login(request);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}