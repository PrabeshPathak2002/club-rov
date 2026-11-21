from glob import glob
from setuptools import setup

setup(
    name='club_rov_navigation', version='0.1.0',
    packages=['club_rov_navigation'],
    data_files=[
        ('share/ament_index/resource_index/packages', ['resource/club_rov_navigation']),
        ('share/club_rov_navigation', ['package.xml']),
        ('share/club_rov_navigation/launch', glob('launch/*.launch.py')),
        ('share/club_rov_navigation/config', glob('config/*.yaml')),
    ],
    install_requires=['setuptools'], zip_safe=True,
    tests_require=['pytest'],
    maintainer='Club ROV maintainers', maintainer_email='maintainers@example.invalid',
    description='Simulated DVL, IMU and depth measurements for ROS 2 Humble.',
    license='Proprietary',
    entry_points={'console_scripts': ['sim_sensors = club_rov_navigation.sim_sensors:main',
                                    'external_nav = club_rov_navigation.external_nav:main',
                                    'cerulean_dvl = club_rov_navigation.cerulean_dvl:main',
                                    'sim_mission = club_rov_navigation.sim_mission:main',
                                    'prequal_mission = club_rov_navigation.prequal_mission:main',
                                    'prequal_record = club_rov_navigation.prequal_record:main',
                                    'prequal_review = club_rov_navigation.prequal_review:main',
                                    'calibrate_camera = club_rov_navigation.calibrate_camera:main']},
)
