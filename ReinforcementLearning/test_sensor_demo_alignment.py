"""Protect the cross-version action timing and episode boundary conversion."""
import importlib.util
from pathlib import Path
import unittest
from mlagents_envs.communicator_objects.agent_info_action_pair_pb2 import AgentInfoActionPairProto

spec = importlib.util.spec_from_file_location('align', Path(__file__).with_name('align-sensor-demos.py'))
align = importlib.util.module_from_spec(spec)
spec.loader.exec_module(align)


def pair(action, done=False, agent=0):
    value = AgentInfoActionPairProto()
    value.agent_info.done = done
    value.agent_info.id = agent
    value.action_info.continuous_actions.extend([action] * 4)
    return value


class AlignmentTests(unittest.TestCase):
    def test_labels_shift_forward_without_crossing_terminal(self):
        source = [pair(0), pair(.2), pair(.4, True), pair(0, agent=1), pair(.8, True, agent=1), pair(0)]
        result = align.align_pairs(source)
        self.assertEqual(len(result), 5)  # incomplete trailing episode removed
        for item, expected in zip(result, [.2, .4, 0, .8, 0]):
            self.assertAlmostEqual(item.action_info.continuous_actions[0], expected)
        self.assertEqual(source[0].action_info.continuous_actions[0], 0)
        self.assertTrue(result[2].agent_info.done)

    def test_incomplete_or_interleaved_recording_is_rejected(self):
        with self.assertRaises(ValueError):
            align.align_pairs([pair(0), pair(.2)])
        with self.assertRaises(ValueError):
            align.align_pairs([pair(0), pair(.2, agent=1), pair(.4, True, agent=1)])

    def test_failed_episode_excluded_as_a_whole(self):
        failed, success = pair(.2, True), pair(.8, True, agent=1)
        failed.agent_info.reward = -1
        success.agent_info.reward = 7
        source = [pair(0), failed, pair(0, agent=1), success]
        result = align.align_pairs(source, include_failures=False)
        self.assertEqual(len(result), 2)
        self.assertEqual(result[0].agent_info.id, 1)
        self.assertAlmostEqual(result[0].action_info.continuous_actions[0], .8)
        with self.assertRaises(ValueError):
            align.align_pairs(source[:2], include_failures=False)


if __name__ == '__main__':
    unittest.main()
