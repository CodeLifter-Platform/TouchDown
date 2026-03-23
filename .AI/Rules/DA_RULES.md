- DA stands for DataAccessor and that is all these should do, connect a service directly to the DB using an injected DBContextFactory.
- DA Should implement Serilog Stuructured logging
- DAs must have a complete interface applied to all public methods
- Da should have its own custom exception
- DA should be shaped like 
      Interface
      Custom exception
      DA Class