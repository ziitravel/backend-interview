using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Microsoft.ApplicationInsights;
using Newtonsoft.Json;
using Zii.Core.API.Constants;
using Zii.Core.API.Exceptions;
using Zii.Core.API.Extensions;
using Zii.Core.API.Models;
using Zii.Trip.API.Extensions;
using Zii.Trip.API.Models.Requests;
using Zii.Trip.API.Queries;
using Zii.Trip.API.Services.Interfaces;
using Zii.Trip.Common.Application.Commands.UpdateTrip;
using Zii.Trip.Common.Application.Common.Constants;
using Zii.Trip.Common.Application.Common.Extensions;
using Zii.Trip.Common.Application.Common.Interfaces;
using Zii.Trip.Common.Application.Requests;
using Zii.Trip.Common.Domain.Models;
using Zii.Trip.Common.Domain.ValueObjects;
using Zii.Trip.Common.Infrastructure.Extensions;
using Zii.Trip.Common.Generated.Models;
using TripSearchElement = Zii.Trip.Common.Domain.ValueObjects.TripSearchElement;
using TripStatus = Zii.Trip.Common.Domain.ValueObjects.TripStatus;

namespace Zii.Trip.API.Services;

public class TripService : BaseService, ITripService
{
    private readonly IErrorFactory _errorFactory;
    private readonly IMapper _mapper;
    private readonly ITripRepository _tripRepository;
    private readonly ITripSearchRepository _tripSearchRepository;
    private readonly ITripMetaRepository _tripMetaRepository;
    private readonly IBookingMetaRepository _bookingMetaRepository;

    public TripService(ISender mediatorSender, IMapper mapper, IErrorFactory errorFactory, TelemetryClient telemetry,
        ITripSearchRepository tripSearchRepository, ITripRepository tripRepository, ITripMetaRepository tripMetaRepository,
        IBookingMetaRepository bookingMetaRepository) :
        base(telemetry, mediatorSender)
    {
        _mapper = mapper;
        _errorFactory = errorFactory;
        _tripSearchRepository = tripSearchRepository;
        _tripRepository = tripRepository;
        _tripMetaRepository = tripMetaRepository;
        _bookingMetaRepository = bookingMetaRepository;
    }

    public async Task<Common.Generated.Models.Trip> GetTripAsync(Guid tripId)
    {
        var trip = await _tripRepository.GetTripByIdAsync(tripId) ?? throw new ZiiNotFoundException();
        var tripMetaTripIssues = await _tripMetaRepository.GetTripMetaAsync(tripId, TripMetaKeyValues.TripIssues);
    
        var tripModel = _mapper.Map<Common.Generated.Models.Trip>(trip);
    
        // Because we want the model to have the obt trip id set and we need to get it from meta values related to the trip entity
        var bookings = trip.TripBookingRelations.Select(tbr => tbr.Booking).AsEnumerable().ToList();
        var segmentLookup = tripModel.Segments
            .Where(s => s.ExternalBookingId != null)
            .ToLookup(s => s.ExternalBookingId);
    
        foreach (var booking in bookings)
        {
            var bookingCertifyTripMeta =
                await _bookingMetaRepository
                    .GetTripMetasAsync(booking.Id, BookingMetaKey.CertifyTripId)
                    .ConfigureAwait(false);
    
            if (bookingCertifyTripMeta?.MetaValue == null)
            {
                continue;
            }
    
            var relatedSegments = segmentLookup[booking.ExternalId];
            foreach (var segment in relatedSegments)
            {
                segment.ObtTripId = bookingCertifyTripMeta.MetaValue;
            }
        }
    
        tripModel.SetTripModelIsCancellableOnBookings(trip);
    
        await tripModel.SetCheckInUrl(_tripRepository);
    
        // Here we set canceled segments in the model regardless of the trip status
        await SetCanceledSegments(tripModel, _tripRepository, trip, _mapper).ConfigureAwait(false);
    
        // Here we set the documents at the tripModel root level given that we want every documents regardless of segment status.
        await tripModel.SetTripDocuments(_tripRepository, trip).ConfigureAwait(false);
    
        tripModel.Owner ??= tripModel.Traveler;
        tripModel.TripIssues = tripMetaTripIssues != null
            ? JsonConvert.DeserializeObject<List<TripIssue>>(tripMetaTripIssues.MetaValue) : new List<TripIssue>();
    
        return tripModel;
    }
    
    private void SetSegmentsObtTripIds(TripEntity trip, Common.Generated.Models.Trip tripModel)
    {
        foreach (segment in tripModel.segments)
        {
            segment.obtTripId = trip.obtTripId
          }
    }
    
    
    private void SetCanceledSegments(Common.Generated.Models.Trip tripModel, ITripRepository tripRepo, TripEntity trip, IMapper mapper)
    {
        if (tripEntity.TripBookingRelations.Count != 0 && trip.Status == TripStatus.CanceledEnum)
        {
            var bookingRecordId = tripEntity.TripBookingRelations[0].BookingRecordId;
            var bookings = await tripRepository.GetBookingsByBookingRecordIdOrderedByVersionDescAsync(bookingRecordId);
            var latestBookingWithSegments = bookings.FirstOrDefault(b => b.Segments.Count != 0);
            tripModel.CanceledSegments = mapper.Map<List<Common.Domain.Entities.Segment>, List<Segment>>(latestBookingWithSegments?.Segments);
            return;
        }
    
        tripModel.CanceledSegments = new List<Segment>();
    }
    
    public static async Task SetTripDocuments(
        this Common.Generated.Models.Trip trip,
        ITripRepository tripRepository,
        TripEntity tripEntity)
    {
        var bookingRecordIds = tripEntity.TripBookingRelations.Select(b => b.BookingRecordId).ToList();
        var documents = new List<CompleteDocument>();
        foreach (var bookingRecordId in bookingRecordIds)
        {
            var tripBookingRelation = tripEntity.TripBookingRelations.FirstOrDefault(b => b.BookingRecordId == bookingRecordId);
            if (tripBookingRelation == null)
            {
                return;
            }
    
            var pnrStatus = tripBookingRelation.Booking.Segments.ToList().GetPnrStatus();
            var invoices = await tripRepository.GetInvoicesByBookingRecordIdAsync(bookingRecordId);
            var itineraries = await tripRepository.GetItineraryByBookingRecordIdAsync(bookingRecordId);
    
            var latestInvoice = invoices?.OrderByDescending(i => i.Version).FirstOrDefault();
            var latestItinerary = itineraries?.OrderByDescending(i => i.Version).FirstOrDefault();
    
            var latestInvoiceDocument = latestInvoice?.Documents.FirstOrDefault();
            var latestItineraryDocument = latestItinerary?.Documents.FirstOrDefault();
    
            documents.AddInvoice(latestInvoice, latestInvoiceDocument, pnrStatus, tripBookingRelation);
            documents.AddItinerary(latestItinerary, latestItineraryDocument, pnrStatus, tripBookingRelation);
        }
    
        trip.Documents = documents;
    }
}
