"""Regression checks for completion-driven advancement independent of PPO reward."""
import importlib.util
from pathlib import Path
import unittest

from mlagents.trainers.settings import RunOptions
from mlagents.plugins.trainer_type import register_trainer_plugins
import yaml

module_spec = importlib.util.spec_from_file_location('course_trainer', Path(__file__).with_name('course-trainer.py'))
trainer = importlib.util.module_from_spec(module_spec)
module_spec.loader.exec_module(trainer)


class CompletionCurriculumTests(unittest.TestCase):
    def setUp(self):
        register_trainer_plugins()
        self.config = RunOptions.from_dict(yaml.safe_load(Path(__file__).with_name('pool-course.yaml').read_text()))
        trainer.completion_window.values.clear()
        self.manager = trainer.CompletionCurriculum(self.config.environment_parameters)

    def update(self, reward=1000):
        return self.manager.update_lessons({'PoolCourse': 100_000}, {'PoolCourse': 10_000_000},
                                          {'PoolCourse': [reward] * 200})

    def test_no_completion_samples_never_advance_from_shaped_reward(self):
        self.assertEqual(self.update(), (False, False))

    def test_hold_practice_success_does_not_advance_full_course_curriculum(self):
        for _ in range(250):
            trainer.completion_window.on_add_stat('PoolCourse', 'Practice/HoverCompleted', 1.0)
            trainer.completion_window.on_add_stat('PoolCourse', 'Practice/DockCompleted', 1.0)
        self.assertEqual(len(trainer.completion_window.values), 0)
        self.assertEqual(self.update(), (False, False))

    def test_minimum_200_finished_episodes(self):
        for _ in range(199):
            trainer.completion_window.on_add_stat('PoolCourse', 'Course/Completed', 1.0)
        self.assertEqual(self.update(), (False, False))

    def test_failures_and_objectives_do_not_count_as_completions(self):
        for _ in range(200):
            trainer.completion_window.on_add_stat('PoolCourse', 'Course/Objectives', 4.0)
            trainer.completion_window.on_add_stat('PoolCourse', 'Course/Completed', 0.0)
        self.assertEqual(self.update(), (False, False))

    def test_success_advances_once_then_requires_fresh_episodes(self):
        for _ in range(200):
            trainer.completion_window.on_add_stat('PoolCourse', 'Course/Completed', 1.0)
        self.assertEqual(self.update(reward=-1000), (True, True))
        self.assertEqual(self.manager.get_current_lesson_number()['course_randomisation'], 1)
        self.assertEqual(len(trainer.completion_window.values), 0)
        self.assertEqual(self.update(), (False, False))

    def test_invalid_completion_is_rejected(self):
        for value in (0.5, float('nan'), float('inf')):
            with self.assertRaises(ValueError):
                trainer.completion_window.on_add_stat('PoolCourse', 'Course/Completed', value)

    def test_sensor_behavior_isolated_and_teacher_rejected(self):
        window = trainer.CompletionWindow('PoolCourseSensors')
        window.on_add_stat('PoolCourse', 'Course/Completed', 1.0)
        self.assertEqual(list(window.values), [])
        window.on_add_stat('PoolCourseSensors', 'Course/Completed', 1.0)
        self.assertEqual(list(window.values), [1.0])
        window.on_add_stat('PoolCourseSensors', 'Sensor/TeacherActive', 0.0)
        with self.assertRaises(RuntimeError):
            window.on_add_stat('PoolCourseSensors', 'Sensor/TeacherActive', 1.0)

    def test_sensor_curriculum_uses_sensor_completion(self):
        original = trainer.completion_window
        try:
            trainer.completion_window = trainer.CompletionWindow('PoolCourseSensors')
            config = RunOptions.from_dict(yaml.safe_load(Path(__file__).with_name('pool-course-sensors.yaml').read_text()))
            manager = trainer.CompletionCurriculum(config.environment_parameters)
            for _ in range(200):
                trainer.completion_window.on_add_stat('PoolCourseSensors', 'Course/Completed', 1.0)
            self.assertEqual(manager.update_lessons({'PoolCourseSensors': 100_000},
                {'PoolCourseSensors': 10_000_000}, {'PoolCourseSensors': [-1000] * 200}), (True, True))
            self.assertEqual(len(trainer.completion_window.values), 0)
        finally:
            trainer.completion_window = original


if __name__ == '__main__':
    unittest.main()
