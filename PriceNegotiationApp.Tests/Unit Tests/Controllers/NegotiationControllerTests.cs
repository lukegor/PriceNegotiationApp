using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using NSubstitute;
using PriceNegotiationApp.Controllers;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Models.Input_Models;
using PriceNegotiationApp.Services;
using PriceNegotiationApp.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace PriceNegotiationApp.Tests.Unit_Tests.Controllers
{
    public class NegotiationControllerTests
    {
        private readonly ITestOutputHelper _output;

        public NegotiationControllerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task GetNegotiations_ShouldReturnListOfNegotiations()
        {
            // Arrange
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            var negotiations = new List<Negotiation>
            {
                new Negotiation("123", 50, "user1") { RetriesLeft = 2 },
                new Negotiation("321", 40, "user2") { RetriesLeft = 2 }
            };

            negotiationService.GetNegotiationsAsync().Returns(Task.FromResult<IEnumerable<Negotiation>>(negotiations));

            // Act
            var result = await controller.GetNegotiations();

            // Assert
            var okObjectResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(200, okObjectResult.StatusCode);

            var returnedNegotiations = Assert.IsType<List<Negotiation>>(okObjectResult.Value);
            Assert.Equal(negotiations, returnedNegotiations);

            negotiationService.Received(1).GetNegotiationsAsync();
        }

        [Fact]
        public async Task GetNegotiation_ExistingId_ShouldReturnNegotiation()
        {
            // Arrange
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            int negotiationId = 1111;
            var expectedNegotiation = new Negotiation("123", 50, "user1") { RetriesLeft = 2 };

            negotiationService.GetNegotiationAsync(negotiationId).Returns(Task.FromResult(expectedNegotiation));

            // Act
            var result = await controller.GetNegotiation(negotiationId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<Negotiation>>(result);

            var okObjectResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Equal(200, okObjectResult.StatusCode);

            var returnedNegotiation = Assert.IsType<Negotiation>(okObjectResult.Value);
            Assert.Equal(expectedNegotiation, returnedNegotiation);

            negotiationService.Received(1).GetNegotiationAsync(negotiationId);
        }

        [Fact]
        public async Task GetNegotiation_NonExistingId_ShouldReturnNotFound()
        {
            // Arrange
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            int negotiationId = 999; // Non-existing ID

            negotiationService.GetNegotiationAsync(negotiationId).Returns(Task.FromResult<Negotiation>(null));

            // Act
            var result = await controller.GetNegotiation(negotiationId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<Negotiation>>(result);

            var notFoundResult = Assert.IsType<NotFoundResult>(actionResult.Result);
            Assert.Equal(404, notFoundResult.StatusCode);

            negotiationService.Received(1).GetNegotiationAsync(negotiationId);
        }

        [Fact]
        public async Task PostNegotiation_ValidInput_ShouldReturnCreatedAtAction()
        {
            // Arrange
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            var negotiationInputModel = new NegotiationInputModel
            {
                ProductId = "123",
                ProposedPrice = 50
            };

            negotiationService.CreateNegotiationAsync(new NegotiationInputModel { ProductId = "123", ProposedPrice = 50 })
                .Returns(Task.FromResult(new Negotiation("123", 50, "user1") { RetriesLeft = 2 }));

            // Act
            var result = await controller.PostNegotiation(negotiationInputModel);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(201, createdAtActionResult.StatusCode);

            Assert.Equal(nameof(NegotiationController.GetNegotiation), createdAtActionResult.ActionName);
            Assert.Equal(1, createdAtActionResult.RouteValues["id"]);

            var returnedNegotiation = Assert.IsType<Negotiation>(createdAtActionResult.Value);
            Assert.Equal("123", returnedNegotiation.ProductId);
            Assert.Equal(50, returnedNegotiation.ProposedPrice);
            Assert.Equal("user1", returnedNegotiation.UserId);

            negotiationService.Received(1).CreateNegotiationAsync(Arg.Any<NegotiationInputModel>());
        }

        [Fact]
        public async Task PostNegotiation_InvalidInput_ShouldReturnBadRequest()
        {
            // Arrange
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);
            controller.ModelState.AddModelError("PropertyName", "Error Message");

            var negotiationInputModel = new NegotiationInputModel()
            {
                ProductId = "322",
                ProposedPrice = 50
            };

            // Act
            var result = await controller.PostNegotiation(negotiationInputModel);

            // Assert
            var badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(400, badRequestObjectResult.StatusCode);

            var errorMessages = Assert.IsType<List<object>>(badRequestObjectResult.Value);
            Assert.NotEmpty(errorMessages);

            negotiationService.DidNotReceive().CreateNegotiationAsync(Arg.Any<NegotiationInputModel>());
        }

        [Fact]
        public async Task DeleteNegotiation_ExistingId_ShouldReturnNoContent()
        {
            // Arrange
            int negotiationId = 1111;
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            negotiationService.DeleteNegotiationAsync(negotiationId)
                .Returns(Task.FromResult(true));

            // Act
            var result = await controller.DeleteNegotiation(negotiationId);

            // Assert
            Assert.IsType<NoContentResult>(result);
            negotiationService.Received(1).DeleteNegotiationAsync(negotiationId);
        }

        [Fact]
        public async Task DeleteNegotiation_NonExistingId_ShouldReturnNotFound()
        {
            // Arrange
            int negotiationId = 999; // Non-existing ID
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            negotiationService.DeleteNegotiationAsync(negotiationId).Returns(Task.FromResult(false));

            // Act
            var result = await controller.DeleteNegotiation(negotiationId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
            negotiationService.Received(1).DeleteNegotiationAsync(negotiationId);
        }

        [Fact]
        public async Task PutNegotiation_ValidInput_ShouldReturnNoContent()
        {
            // Arrange
            int negotiationId = 1111;
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            var negotiation = new Negotiation("123", 50, "user1") { RetriesLeft = 2 };

            negotiationService.UpdateNegotiationAsync(negotiationId, Arg.Any<Negotiation>())
                .Returns(Task.FromResult(UpdateResultType.Success));

            // Act
            var result = await controller.PutNegotiation(negotiationId, negotiation);

            // Assert
            Assert.IsType<NoContentResult>(result);
            negotiationService.Received(1).UpdateNegotiationAsync(negotiationId, Arg.Any<Negotiation>());
        }

        [Fact]
        public async Task PutNegotiation_NotFound_ShouldReturnNotFound()
        {
            // Arrange
            int negotiationId = 1111;
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            var negotiation = new Negotiation("123", 50, "user1") { RetriesLeft = 2 };

            negotiationService.UpdateNegotiationAsync(negotiationId, Arg.Any<Negotiation>()).Returns(Task.FromResult(UpdateResultType.NotFound));

            // Act
            var result = await controller.PutNegotiation(negotiationId, negotiation);

            // Assert
            Assert.IsType<NotFoundResult>(result);
            negotiationService.Received(1).UpdateNegotiationAsync(negotiationId, Arg.Any<Negotiation>());
        }

        [Fact]
        public async Task PutNegotiation_Conflict_ShouldReturnConflict()
        {
            // Arrange
            int negotiationId = 1111;
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            var negotiation = new Negotiation("123", 50, "user1") { RetriesLeft = 2 };

            negotiationService.UpdateNegotiationAsync(negotiationId, Arg.Any<Negotiation>()).Returns(Task.FromResult(UpdateResultType.Conflict));

            // Act
            var result = await controller.PutNegotiation(negotiationId, negotiation);

            // Assert
            Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal("Concurrency conflict", (result as ConflictObjectResult)?.Value);
            negotiationService.Received(1).UpdateNegotiationAsync(negotiationId, Arg.Any<Negotiation>());
        }

        [Fact]
        public async Task PutNegotiation_InternalServerError_ShouldReturnInternalServerError()
        {
            // Arrange
            int negotiationId = 1111;
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            var negotiation = new Negotiation("123", 50, "user1") { RetriesLeft = 2 };

            negotiationService.UpdateNegotiationAsync(negotiationId, Arg.Any<Negotiation>()).Returns(Task.FromResult((UpdateResultType)100)); // Assuming 100 represents an unknown error

            // Act
            var result = await controller.PutNegotiation(negotiationId, negotiation);

            // Assert
            Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, (result as ObjectResult)?.StatusCode);
            Assert.Equal("Internal Server Error", (result as ObjectResult)?.Value);
            negotiationService.Received(1).UpdateNegotiationAsync(negotiationId, Arg.Any<Negotiation>());
        }

        [Fact]
        public async Task ProposeNewPrice_ValidInput_ShouldReturnOk()
        {
            // Arrange
            int negotiationId = 1111;
            decimal proposedPrice = 60;
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            var response = new ProposePriceResponse
            {
                Result = ProposePriceResult.Success,
                MaxAllowedPrice = 70
            };

            negotiationService.ProposeNewPriceAsync(negotiationId, proposedPrice).Returns(Task.FromResult(response));

            // Act
            var result = await controller.ProposeNewPrice(negotiationId, proposedPrice);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Price proposed successfully.", (result as OkObjectResult)?.Value);
            negotiationService.Received(1).ProposeNewPriceAsync(negotiationId, proposedPrice);
        }

        [Fact]
        public async Task ProposeNewPrice_NotFound_ShouldReturnNotFound()
        {
            // Arrange
            int negotiationId = 1111;
            decimal proposedPrice = 60;
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            var response = new ProposePriceResponse
            {
                Result = ProposePriceResult.NotFound
            };

            negotiationService.ProposeNewPriceAsync(negotiationId, proposedPrice).Returns(Task.FromResult(response));

            // Act
            var result = await controller.ProposeNewPrice(negotiationId, proposedPrice);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Negotiation not found.", (result as NotFoundObjectResult)?.Value);
            negotiationService.Received(1).ProposeNewPriceAsync(negotiationId, proposedPrice);
        }

        [Fact]
        public async Task ProposeNewPrice_Unauthorized_ShouldReturnForbid()
        {
            // Arrange
            int negotiationId = 1111;
            decimal proposedPrice = 60;
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            var response = new ProposePriceResponse
            {
                Result = ProposePriceResult.Unauthorized
            };

            negotiationService.ProposeNewPriceAsync(negotiationId, proposedPrice).Returns(Task.FromResult(response));

            // Act
            var result = await controller.ProposeNewPrice(negotiationId, proposedPrice);

            // Assert
            Assert.IsType<ForbidResult>(result);
            negotiationService.Received(1).ProposeNewPriceAsync(negotiationId, proposedPrice);
        }

        [Fact]
        public async Task ProposeNewPrice_IncorrectAction_ShouldReturnBadRequest()
        {
            // Arrange
            int negotiationId = 1111;
            decimal proposedPrice = 60;
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            var response = new ProposePriceResponse
            {
                Result = ProposePriceResult.IncorrectAction
            };

            negotiationService.ProposeNewPriceAsync(negotiationId, proposedPrice).Returns(Task.FromResult(response));

            // Act
            var result = await controller.ProposeNewPrice(negotiationId, proposedPrice);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("No more retries are left for this negotiation.", (result as BadRequestObjectResult)?.Value);
            negotiationService.Received(1).ProposeNewPriceAsync(negotiationId, proposedPrice);
        }

        [Fact]
        public async Task ProposeNewPrice_InvalidInput_ShouldReturnBadRequestWithMessage()
        {
            // Arrange
            int negotiationId = 1111;
            decimal proposedPrice = 60;
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            var response = new ProposePriceResponse
            {
                Result = ProposePriceResult.InvalidInput,
                MaxAllowedPrice = 2 * proposedPrice
            };

            negotiationService.ProposeNewPriceAsync(negotiationId, proposedPrice).Returns(Task.FromResult(response));

            // Act
            var result = await controller.ProposeNewPrice(negotiationId, proposedPrice);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal($"Invalid proposed price. Please note that the proposed price should be within the range of 0.01 - {response.MaxAllowedPrice}.", (result as BadRequestObjectResult)?.Value);
            negotiationService.Received(1).ProposeNewPriceAsync(negotiationId, proposedPrice);
        }

        [Fact]
        public async Task ProposeNewPrice_InternalServerError_ShouldReturnInternalServerError()
        {
            // Arrange
            int negotiationId = 1111;
            decimal proposedPrice = 60;
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            var response = new ProposePriceResponse
            {
                Result = (ProposePriceResult)100 // Assuming 100 represents an unknown error
            };

            negotiationService.ProposeNewPriceAsync(negotiationId, proposedPrice).Returns(Task.FromResult(response));

            // Act
            var result = await controller.ProposeNewPrice(negotiationId, proposedPrice);

            // Assert
            Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, (result as ObjectResult)?.StatusCode);
            Assert.Equal("An error occurred while processing the proposal.", (result as ObjectResult)?.Value);
            negotiationService.Received(1).ProposeNewPriceAsync(negotiationId, proposedPrice);
        }

        [Fact]
        public async Task RespondToNegotiationProposal_Success_ShouldReturnOkWithAcceptedMessage()
        {
            // Arrange
            int negotiationId = 1111;
            bool isApproved = true;
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            negotiationService.RespondToNegotiationProposalAsync(negotiationId, isApproved).Returns(Task.FromResult(UpdateResultType.Success));

            // Act
            var result = await controller.RespondToNegotiationProposal(negotiationId, isApproved);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Proposal accepted", (result as OkObjectResult)?.Value);
            negotiationService.Received(1).RespondToNegotiationProposalAsync(negotiationId, isApproved);
        }

        [Fact]
        public async Task RespondToNegotiationProposal_SuccessRejected_ShouldReturnOkWithRejectedMessage()
        {
            // Arrange
            int negotiationId = 1111;
            bool isApproved = false;
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            negotiationService.RespondToNegotiationProposalAsync(negotiationId, isApproved).Returns(Task.FromResult(UpdateResultType.Success));

            // Act
            var result = await controller.RespondToNegotiationProposal(negotiationId, isApproved);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Proposal rejected", (result as OkObjectResult)?.Value);
            negotiationService.Received(1).RespondToNegotiationProposalAsync(negotiationId, isApproved);
        }

        [Fact]
        public async Task RespondToNegotiationProposal_NotFound_ShouldReturnNotFound()
        {
            // Arrange
            int negotiationId = 1111;
            bool isApproved = true; // doesn't matter in this case
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            negotiationService.RespondToNegotiationProposalAsync(negotiationId, isApproved).Returns(Task.FromResult(UpdateResultType.NotFound));

            // Act
            var result = await controller.RespondToNegotiationProposal(negotiationId, isApproved);

            // Assert
            Assert.IsType<NotFoundResult>(result);
            negotiationService.Received(1).RespondToNegotiationProposalAsync(negotiationId, isApproved);
        }

        [Fact]
        public async Task RespondToNegotiationProposal_Conflict_ShouldReturnBadRequest()
        {
            // Arrange
            int negotiationId = 1111;
            bool isApproved = true; // doesn't matter in this case
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            negotiationService.RespondToNegotiationProposalAsync(negotiationId, isApproved).Returns(Task.FromResult(UpdateResultType.Conflict));

            // Act
            var result = await controller.RespondToNegotiationProposal(negotiationId, isApproved);

            // Assert
            Assert.IsType<BadRequestResult>(result);
            negotiationService.Received(1).RespondToNegotiationProposalAsync(negotiationId, isApproved);
        }

        [Fact]
        public async Task RespondToNegotiationProposal_InternalServerError_ShouldReturnInternalServerError()
        {
            // Arrange
            int negotiationId = 1111;
            bool isApproved = true; // doesn't matter in this case
            var negotiationService = Substitute.For<INegotiationService>();
            var controller = new NegotiationController(negotiationService);

            negotiationService.RespondToNegotiationProposalAsync(negotiationId, isApproved).Returns(Task.FromResult((UpdateResultType)100)); // Assuming 100 represents an unknown error

            // Act
            var result = await controller.RespondToNegotiationProposal(negotiationId, isApproved);

            // Assert
            Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, (result as ObjectResult)?.StatusCode);
            Assert.Equal("Internal Server Error", (result as ObjectResult)?.Value);
            negotiationService.Received(1).RespondToNegotiationProposalAsync(negotiationId, isApproved);
        }
    }
}
