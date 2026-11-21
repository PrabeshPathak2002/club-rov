# Velocity at the acoustic measurement origin in header.frame_id, m/s.
# Invalid samples are status reports, never zero-velocity measurements.
std_msgs/Header header
geometry_msgs/Vector3 velocity
float64[9] velocity_covariance
bool valid
uint8 beam_count
# Approximate sensor-plane altitude, metres; zero means unavailable.
float64 altitude
string status
