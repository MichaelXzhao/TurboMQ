# TurboMQ

<img src="/images/logo.png" alt="logo" width="400" height="300">

## System Design

<img src="/images/systemDesign.png" alt="systemDesign" width="700" height="400">

## Sprint 1

- Implemented the basic message queue model for both MQ server and client.

<img src="/images/simple.png" alt="simple" width="700" height="400">

## Sprint 2

- Realized the Pub/Sub message queue model.
- Implemented the Broadcast functionality.
- Considered message persistence: Implemented message storage in the Log File and retrieval logic, ensuring messages won't be lost if the server terminates.
- Designed a management interface using SignalR, allowing administrators to view the queue status and monitor performance.

<img src="/images/PubSub.png" alt="PubSub" width="700" height="400">

## Sprint 3

- Implemented the ACK mechanism, handling the logic of message confirmation.
- Implemented the timeout retransmission mechanism: If the confirmation isn't received within the predetermined time, the message should be resent.

<img src="/images/ACK.png" alt="ACK" width="700" height="400">

## Sprint 4

- Realized a distributed architecture to enhance the system's availability and flexibility.
- Implemented the election mechanism using Raft algorithm.
- Contemplated how to carry out failover and load balancing.

<img src="/images/raft1.png" alt="Raft" width="700" height="400">

<img src="/images/raft2.png" alt="Raft" width="700" height="400">

<img src="/images/raft3.png" alt="Raft" width="700" height="400">

<img src="/images/raft4.png" alt="Raft" width="700" height="400">

## Sprint 5

- TurboMQ applied in short URL system

<img src="/images/management.png" alt="Management" width="800" height="500">

<img src="/images/database.png" alt="Database" width="800" height="500">
