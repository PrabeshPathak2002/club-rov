from pathlib import Path
import os
from uuid import uuid4
from ament_index_python.packages import get_package_share_directory
from launch import LaunchDescription
from launch.actions import DeclareLaunchArgument, IncludeLaunchDescription
from launch.conditions import IfCondition
from launch.launch_description_sources import PythonLaunchDescriptionSource
from launch.substitutions import LaunchConfiguration, PathJoinSubstitution, PythonExpression
from launch_ros.actions import Node
from launch_ros.parameter_descriptions import ParameterValue
from launch_ros.substitutions import FindPackageShare


def generate_launch_description():
    config = str(Path(get_package_share_directory('club_rov_navigation'))/'config/local_ekf.yaml')
    enabled = IfCondition(LaunchConfiguration('with_estimator'))
    mapping = LaunchConfiguration('with_mapping')
    return LaunchDescription([
        DeclareLaunchArgument('with_estimator', default_value='true'),
        DeclareLaunchArgument('publish_ground_truth', default_value='false'),
        DeclareLaunchArgument('external_navigation', default_value='false'),
        DeclareLaunchArgument('cerulean_dvl', default_value='false'),
        DeclareLaunchArgument('with_mapping', default_value='false'),
        DeclareLaunchArgument('with_rtabmap', default_value=mapping),
        DeclareLaunchArgument('sensor_tcp_port', default_value='9011'),
        DeclareLaunchArgument('mapping_viewer', default_value='true'),
        DeclareLaunchArgument('mapping_database', default_value=str(Path.home()/f'club-rov-map-{uuid4().hex[:8]}.db')),
        Node(package='club_rov_navigation', executable='external_nav',
             prefix=str(Path(os.environ.get('CLUB_ROV_ROS_WS', str(Path.home()/'club-rov-ros2-ws')))/'mavlink-venv/bin/python'),
             condition=IfCondition(LaunchConfiguration('external_navigation')),
             parameters=[{'use_sim_time': True}], output='screen'),
        Node(package='club_rov_navigation', executable='cerulean_dvl',
             prefix=str(Path(os.environ.get('CLUB_ROV_ROS_WS', str(Path.home()/'club-rov-ros2-ws')))/'mavlink-venv/bin/python'),
             condition=IfCondition(LaunchConfiguration('cerulean_dvl')),
             parameters=[{'use_sim_time': True}], output='screen'),
        Node(package='club_rov_navigation', executable='sim_sensors', output='screen',
             parameters=[{'use_sim_time': True, 'publish_ground_truth':
                          ParameterValue(LaunchConfiguration('publish_ground_truth'), value_type=bool),
                          'with_mapping': ParameterValue(mapping, value_type=bool),
                          'tcp_port': ParameterValue(LaunchConfiguration('sensor_tcp_port'), value_type=int)}]),
        Node(package='imu_filter_madgwick', executable='imu_filter_madgwick_node', name='imu_filter',
             condition=enabled, parameters=[{'use_sim_time': True, 'use_mag': False,
                 'publish_tf': False, 'world_frame': 'enu', 'constant_dt': 0.0,
                 'orientation_stddev': 0.05}], output='screen'),
        Node(package='robot_localization', executable='ekf_node', name='local_ekf', condition=enabled,
             parameters=[config], remappings=[('odometry/filtered', '/odometry/local')], output='screen'),
        IncludeLaunchDescription(
            PythonLaunchDescriptionSource(PathJoinSubstitution(
                [FindPackageShare('rtabmap_launch'), 'launch', 'rtabmap.launch.py'])),
            condition=IfCondition(PythonExpression(
                ["'", mapping, "'.lower() == 'true' and '", LaunchConfiguration('with_rtabmap'), "'.lower() == 'true'"])),
            launch_arguments={
                'use_sim_time': 'true', 'visual_odometry': 'false', 'frame_id': 'base_link',
                'odom_frame_id': 'odom', 'odom_topic': '/odometry/local',
                'rgb_topic': '/mapping/rgb/image', 'depth_topic': '/mapping/depth/image',
                'camera_info_topic': '/mapping/rgb/camera_info', 'approx_sync': 'false',
                'rtabmap_viz': LaunchConfiguration('mapping_viewer'),
                'database_path': LaunchConfiguration('mapping_database'),
                'args': '--Rtabmap/DetectionRate 3 --Grid/3D true --Mem/SaveDepth16Format true',
            }.items()),
    ])
