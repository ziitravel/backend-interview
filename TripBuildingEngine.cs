using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Zii.Core.API.Exceptions;
using Zii.ProfileDataAccess.Application.Queries.Profiles.GetProfileByEmail;
using Zii.ProfileDataAccess.Application.Queries.Profiles.GetProfileById;
using Zii.ProfileDataAccess.Application.Queries.ProfileSyncData.Get;
using Zii.ProfileDataAccess.Application.ReadModels;
using Zii.Trip.Common.Application.Commands.CreateBooking;
using Zii.Trip.Common.Application.Commands.CreateTrip;
using Zii.Trip.Common.Application.Commands.UpdateBooking;
using Zii.Trip.Common.Application.Commands.UpdateTrip;
using Zii.Trip.Common.Application.Common.Extensions;
using Zii.Trip.Common.Application.Common.Interfaces;
using Zii.Trip.Common.Application.Queries.GetBookingById;
using Zii.Trip.Common.Application.Queries.GetGroupTravel;
using Zii.Trip.Common.Application.Queries.GetTripBookingRelationByBookingId;
using Zii.Trip.Common.Application.Queries.SearchTrips;
using Zii.Trip.Common.Domain.Entities;
using Zii.Trip.Common.Domain.Exceptions;
using Zii.Trip.Common.Domain.ValueObjects;
using Zii.Trip.TbfWorker.Consumer.Services;
using Zii.Trip.TbfWorker.Helpers;
using Zii.Trip.TripWorker.BuildingEngine.Extensions;
using Zii.Trip.TripWorker.Shared;
using Zii.Trip.TripWorker.Shared.Models;
using ZiiCompanyAPIClient.Model;
using Booking = Zii.Trip.Common.Domain.Entities.Booking;
using TripEntity = Zii.Trip.Common.Domain.Entities.Trip;
using TripStatus = Zii.Trip.Common.Domain.ValueObjects.TripStatus;

namespace Zii.Trip.TripWorker.BuildingEngine.Services;

public class TripsBuildingEngine : ITripsBuildingEngine
{
    private readonly ISender _mediatorSender;

    public TripsBuildingEngine(ISender mediatorSender)
    {
        _mediatorSender = mediatorSender;
    }
    
    private async Task<TripEntity> CreateNewTrip(
        Booking booking,
        CreateTripCommand createTripCommand,
        List<CreateBookingMetaCommand> additionalBookingMeta)
    {
        await _mediatorSender.Send(new UpdateBookingCommand
        {
            BookingId = booking.Id,
            BookingMetas = additionalBookingMeta
        });
    
        // This will handle the trip creation
        var createdTrip = await _mediatorSender.Send(createTripCommand);
    
        return createdTrip;
    }
}
