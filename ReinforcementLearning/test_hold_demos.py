import unittest
import importlib.util
from pathlib import Path
from mlagents_envs.communicator_objects.agent_info_action_pair_pb2 import AgentInfoActionPairProto
from prepare_hold_demos import hold_clips


class HoldClipTests(unittest.TestCase):
    def test_practice_eval_cannot_read_full_course_success(self):
        spec = importlib.util.spec_from_file_location('evaluate', Path(__file__).with_name('evaluate-course.py'))
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        self.assertEqual(module.completion_metric('full'), 'Course/Completed')
        self.assertEqual(module.completion_metric('hover'), 'Practice/HoverCompleted')
        self.assertEqual(module.completion_metric('dock'), 'Practice/DockCompleted')
    def test_clips_preserve_observations_and_do_not_include_hover_exit_turn(self):
        pairs = []
        for index in range(151):
            pair = AgentInfoActionPairProto()
            pair.agent_info.reward = 2 if index in (10, 25, 40, 90) else 7 if index == 150 else 0
            pair.agent_info.done = index == 150
            pair.action_info.continuous_actions.extend([index / 200.] * 4)
            obs = pair.agent_info.observations.add()
            obs.shape.extend([1])
            obs.float_data.data.extend([float(index)])
            pairs.append(pair)
        clips = dict(hold_clips(pairs))
        self.assertEqual(len(clips['hover']), 30)
        self.assertEqual(clips['hover'][0].agent_info.observations[0].float_data.data[0], 60)
        self.assertEqual(clips['hover'][-1].agent_info.observations[0].float_data.data[0], 89)
        self.assertEqual(clips['dock'][0].agent_info.observations[0].float_data.data[0], 121)
        self.assertAlmostEqual(clips['hover'][0].action_info.continuous_actions[0], .3)
        self.assertTrue(clips['hover'][-1].agent_info.done)
        self.assertFalse(pairs[89].agent_info.done)
        self.assertEqual(list(clips['hover'][-1].action_info.continuous_actions), [0.] * 4)

    def test_ambiguous_or_incomplete_episode_is_not_labelled(self):
        pair = AgentInfoActionPairProto()
        pair.agent_info.done = True
        pair.agent_info.reward = 7
        self.assertEqual(hold_clips([pair]), [])


if __name__ == '__main__':
    unittest.main()
