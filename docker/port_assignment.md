# Port Assignment Documentation

This document tracks the port assignments for all services in the Design18 system to prevent conflicts and ensure proper service discovery.

## Infrastructure Services

### MongoDB
- **Port**: 27017
- **Service**: Database
- **Access**: Internal only (Docker network)

### RabbitMQ
- **Management UI**: 15672 (http://localhost:15672)
- **AMQP Port**: 5672
- **Service**: Message Broker
- **Credentials**: guest/guest (development)

### Hazelcast
- **Member Port**: 5701
- **Management Center**: 8080 (http://localhost:8080)
- **Service**: Distributed Cache
- **Cluster Name**: EntitiesManager

### OpenTelemetry Collector
- **OTLP gRPC**: 4317
- **OTLP HTTP**: 4318
- **Prometheus Metrics**: 8889 (http://localhost:8889/metrics)
- **Health Check**: 8888 (http://localhost:8888/metrics)

## Manager Services

### Manager.Step
- **HTTP**: 5000
- **HTTPS**: 5001
- **Service**: Step entity management

### Manager.Assignment
- **HTTP**: 5010
- **HTTPS**: 5011
- **Service**: Assignment entity management

### Manager.OrchestratedFlow
- **HTTP**: 5040
- **HTTPS**: 5041
- **Service**: Orchestrated flow entity management

### Manager.Schema
- **HTTP**: 5100
- **HTTPS**: 5101
- **Service**: Schema entity management

### Manager.Address
- **HTTP**: 5120
- **HTTPS**: 5121
- **Service**: Address entity management

### Manager.Delivery
- **HTTP**: 5130
- **HTTPS**: 5131
- **Service**: Delivery entity management

### Manager.Orchestrator
- **HTTP**: 5070
- **HTTPS**: 7070
- **Service**: Orchestration management and coordination
- **Endpoints**:
  - POST /api/Orchestration/start/{orchestratedFlowId}
  - POST /api/Orchestration/stop/{orchestratedFlowId}
  - GET /api/Orchestration/status/{orchestratedFlowId}

## Processor Services

### Processor.File (v1.9.9)
- **HTTP**: 5080
- **HTTPS**: 5081
- **Service**: File processing (legacy version)

### Processor.File (v3.2.1)
- **HTTP**: 5090
- **HTTPS**: 5091
- **Service**: File processing (current version)

## Reserved Ports

The following port ranges are reserved for future services:

- **5100-5109**: Reserved for additional Manager services
- **5110-5119**: Reserved for additional Processor services
- **5120-5129**: Reserved for additional infrastructure services

## Port Conflict Resolution

If you encounter port conflicts:

1. **Check what's using a port**:
   ```bash
   # Windows
   netstat -ano | findstr :PORT_NUMBER
   
   # Linux/Mac
   lsof -i :PORT_NUMBER
   ```

2. **Kill process using port** (if safe to do so):
   ```bash
   # Windows
   taskkill /PID <PID> /F
   
   # Linux/Mac
   kill -9 <PID>
   ```

3. **Update service configuration** to use alternative port if needed

## Service Discovery

All services are configured to communicate using the following patterns:

- **Manager-to-Manager**: HTTP calls using configured URLs in appsettings.json
- **Manager-to-Infrastructure**: Direct connection using localhost ports
- **Processor-to-Infrastructure**: Direct connection using localhost ports
- **Orchestrator-to-Managers**: HTTP calls to gather orchestration data

## Health Check Endpoints

All services expose health check endpoints on their respective ports:

- **Path**: `/health`
- **Method**: GET
- **Response**: 200 OK (healthy) or 503 Service Unavailable (unhealthy)

## Notes

- All HTTP ports are for development use
- HTTPS ports are configured but may require SSL certificate setup
- Docker Compose services use internal networking when running in containers
- Local development uses localhost with the assigned ports
