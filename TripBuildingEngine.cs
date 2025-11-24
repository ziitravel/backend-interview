public class TripsBuildingEngine : ITripsBuildingEngine
{
    private readonly ICertifyWebService _certifyService;
    private readonly ICompanyApiEmployeeRepository _employeeRepository;
    private readonly ISender _mediatorSender;
    private readonly TimeSpan _openEndedTripEnd = TimeSpan.FromDays(7);
    private readonly TimeSpan _openEndedTripStart = TimeSpan.FromDays(-7);
    private readonly ICompanyApiTravelPolicyRepository _travelPolicyRepository;
    private readonly ITripIssuesEnforcerService _tripIssuesEnforcerService;
    private readonly ITripRepository _tripRepository;
    private readonly ITfbApolloConsumerService _tfbApolloConsumerService;

    public TripsBuildingEngine(ISender mediatorSender, ICompanyApiEmployeeRepository employeeRepository,
        ICertifyWebService certifyService, ICompanyApiTravelPolicyRepository travelPolicyRepository,
        ITripIssuesEnforcerService tripIssuesEnforcerService, ITripRepository tripRepository,
        ITfbApolloConsumerService tfbApolloConsumerService)
    {
        _mediatorSender = mediatorSender;
        _employeeRepository = employeeRepository;
        _certifyService = certifyService;
        _travelPolicyRepository = travelPolicyRepository;
        _tripIssuesEnforcerService = tripIssuesEnforcerService;
        _tripRepository = tripRepository;
        _tfbApolloConsumerService = tfbApolloConsumerService;
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
